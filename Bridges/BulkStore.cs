using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using AxarDB.Definitions;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Manages JSONL-based bulk collections stored in the "Bulk/" folder.
    /// Supports lazy loading, in-memory caching (LRU eviction), auto-reload via FileSystemWatcher,
    /// and streaming chunked queries for files exceeding the cache budget.
    /// </summary>
    public class BulkStore : IDisposable
    {
        private static readonly bool Diag = Environment.GetEnvironmentVariable("AXARDB_DIAG") == "1";
        private static void Log(string step, string detail, long ms, long count = -1)
        {
            if (!Diag) return;
            var c = count >= 0 ? $" count={count}" : "";
            Console.WriteLine($"[diag:bulk] {step}{c} | {detail} | {ms} ms");
        }

        private readonly string _bulkPath;
        private readonly string _basePath;
        private readonly ConcurrentDictionary<string, BulkCacheEntry> _cache = new();
        private readonly FileSystemWatcher _watcher;
        private readonly JsonSerializerOptions _jsonOptions;

        // Approximate cap: we track total serialized byte size
        private long _totalCachedBytes = 0;
        private readonly long _maxCacheBytes;

        public BulkStore(string basePath, long maxCacheBytes)
        {
            _basePath = basePath;
            _bulkPath = Path.Combine(basePath, "Bulk");
            if (!Directory.Exists(_bulkPath))
                Directory.CreateDirectory(_bulkPath);

            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _jsonOptions.Converters.Add(new AxarDB.Storage.CustomObjectConverter());
            _maxCacheBytes = maxCacheBytes;

            // FileSystemWatcher for auto-reload
            _watcher = new FileSystemWatcher(_bulkPath, "*.jsonl")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
        }

        private class BulkCacheEntry
        {
            public List<Dictionary<string, object>> Documents { get; set; } = new();
            public DateTime LoadedAt { get; set; }
            public long ApproximateBytes { get; set; }
        }

        // ─── File System Events ───────────────────────────────────────────────

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (!string.IsNullOrEmpty(name))
                InvalidateCache(name);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (_cache.TryRemove(name, out var entry))
                Interlocked.Add(ref _totalCachedBytes, -entry.ApproximateBytes);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            var oldName = Path.GetFileNameWithoutExtension(e.OldName ?? "");
            var newName = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (_cache.TryRemove(oldName, out var entry))
                Interlocked.Add(ref _totalCachedBytes, -entry.ApproximateBytes);
            if (!string.IsNullOrEmpty(newName))
                InvalidateCache(newName);
        }

        private void InvalidateCache(string name)
        {
            if (_cache.TryRemove(name, out var old))
                Interlocked.Add(ref _totalCachedBytes, -old.ApproximateBytes);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns names of all existing JSONL collections.</summary>
        public IEnumerable<string> ListCollections()
        {
            if (!Directory.Exists(_bulkPath)) yield break;
            foreach (var f in Directory.EnumerateFiles(_bulkPath, "*.jsonl"))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        /// <summary>Returns all documents from the named JSONL collection (cached).</summary>
        public IEnumerable<Dictionary<string, object>> GetDocuments(string name)
        {
            if (_cache.TryGetValue(name, out var entry))
                return entry.Documents;

            var sw = Stopwatch.StartNew();
            var r = LoadAndCache(name);
            sw.Stop();
            Log("GetDocuments(miss)", name, sw.ElapsedMilliseconds);
            return r;
        }

        /// <summary>Insert/append documents to a JSONL file, creating it if it doesn't exist.</summary>
        public void Insert(string name, IEnumerable<Dictionary<string, object>> documents)
        {
            var sw = Stopwatch.StartNew();
            var path = GetFilePath(name);
            var lines = new List<string>();

            foreach (var doc in documents)
            {
                // _id is always generated by the engine — silently remove any caller-supplied value
                doc.Remove("_id");
                doc["_id"] = AxarDB.Helpers.GuidV7.NewGuid().ToString();
                lines.Add(JsonSerializer.Serialize(doc));

                // --- Backup Query: Insert ---
                try
                {
                    var query = $"bulk.{name}.findall(x => x._id == '{doc["_id"]}').delete()";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
                catch (Exception ex)
                {
                    AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for bulk insert: {ex.Message}");
                }
                // -----------------------------
            }

            // Append to file
            File.AppendAllLines(path, lines, Encoding.UTF8);

            // Invalidate cache so it reloads fresh
            InvalidateCache(name);
            sw.Stop();
            Log("Insert", $"{name} docs={lines.Count}", sw.ElapsedMilliseconds, lines.Count);
        }

        /// <summary>Manually reload a specific collection from disk.</summary>
        public void Reload(string name)
        {
            InvalidateCache(name);
            LoadAndCache(name);
        }

        /// <summary>Reload all collections.</summary>
        public void ReloadAll()
        {
            foreach (var name in ListCollections())
                Reload(name);
        }

        /// <summary>Delete documents matching a predicate and rewrite the JSONL file.</summary>
        public void Delete(string name, Func<Dictionary<string, object>, bool> predicate)
        {
            var docs = GetDocuments(name).ToList();
            var deletedDocs = docs.Where(predicate).ToList();
            var remaining = docs.Where(d => !predicate(d)).ToList();

            // --- Backup Query: Delete ---
            try
            {
                foreach (var doc in deletedDocs)
                {
                    var json = JsonSerializer.Serialize(doc);
                    var query = $"bulk.{name}.insert([{json}])";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for bulk delete: {ex.Message}");
            }
            // -----------------------------

            var path = GetFilePath(name);
            File.WriteAllLines(path, remaining.Select(d => JsonSerializer.Serialize(d)), Encoding.UTF8);
            InvalidateCache(name);
        }

        /// <summary>Update documents matching a predicate and rewrite the JSONL file.</summary>
        public void Update(string name, Func<Dictionary<string, object>, bool> predicate, Dictionary<string, object> fields)
        {
            var docs = GetDocuments(name).ToList();
            bool modified = false;
            foreach (var doc in docs)
            {
                if (predicate(doc))
                {
                    // --- Backup Query: Update ---
                    try
                    {
                        var idObj = doc.GetValueOrDefault("_id");
                        var json = JsonSerializer.Serialize(doc);
                        var query = $"bulk.{name}.findall(x => x._id == '{idObj}').update({json})";
                        AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                    }
                    catch (Exception ex)
                    {
                        AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for bulk update: {ex.Message}");
                    }
                    // -----------------------------

                    foreach (var kv in fields)
                    {
                        if (kv.Key == "_id") continue; // Prevent altering _id
                        doc[kv.Key] = kv.Value;
                    }
                    modified = true;
                }
            }
            if (modified)
            {
                var path = GetFilePath(name);
                File.WriteAllLines(path, docs.Select(d => JsonSerializer.Serialize(d)), Encoding.UTF8);
                InvalidateCache(name);
            }
        }

        /// <summary>
        /// Completely removes a bulk collection by deleting its JSONL file from disk.
        /// After this call the collection will no longer appear in ListCollections().
        /// </summary>
        public void DropCollection(string name)
        {
            // --- Backup Query: DropCollection ---
            try
            {
                var docs = GetDocuments(name).ToList();
                if (docs.Count > 0)
                {
                    var jsonArray = JsonSerializer.Serialize(docs);
                    var query = $"bulk.{name}.insert({jsonArray})";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for bulk drop: {ex.Message}");
            }
            // -----------------------------

            var path = GetFilePath(name);
            if (File.Exists(path))
                File.Delete(path);
            InvalidateCache(name);
        }

        // ─── Internal ─────────────────────────────────────────────────────────

        private IEnumerable<Dictionary<string, object>> LoadAndCache(string name)
        {
            var path = GetFilePath(name);
            if (!File.Exists(path)) return Enumerable.Empty<Dictionary<string, object>>();

            var docs = new List<Dictionary<string, object>>();
            long bytes = 0;

            try
            {
                foreach (var line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(line, _jsonOptions);
                        if (doc != null)
                        {
                            docs.Add(doc);
                            bytes += line.Length * 2;
                        }
                    }
                    catch { /* Skip malformed lines */ }
                }
            }
            catch (IOException) { return Enumerable.Empty<Dictionary<string, object>>(); }

            // Only cache if the file fits within the cache budget
            if (bytes <= _maxCacheBytes)
            {
                EnforceMemoryCap(bytes);

                var entry = new BulkCacheEntry
                {
                    Documents = docs,
                    LoadedAt = ServerTime.Now,
                    ApproximateBytes = bytes
                };

                _cache[name] = entry;
                Interlocked.Add(ref _totalCachedBytes, bytes);
            }

            return docs;
        }

        private void EnforceMemoryCap(long incomingBytes)
        {
            if (_totalCachedBytes + incomingBytes <= _maxCacheBytes) return;

            // Evict oldest entries until we have room
            var ordered = _cache.OrderBy(kv => kv.Value.LoadedAt).ToList();
            foreach (var kv in ordered)
            {
                if (_totalCachedBytes + incomingBytes <= _maxCacheBytes) break;
                if (_cache.TryRemove(kv.Key, out var removed))
                    Interlocked.Add(ref _totalCachedBytes, -removed.ApproximateBytes);
            }
        }

        public IEnumerable<Dictionary<string, object>> QueryChunks(string name, HashSet<string> filterFields, Func<Dictionary<string, object>, bool> predicate)
        {
            // Prefer in-memory cache if available – avoids re-reading from disk
            if (_cache.TryGetValue(name, out var cached))
            {
                foreach (var doc in cached.Documents)
                {
                    if (predicate(doc))
                        yield return doc;
                }
                yield break;
            }

            var path = GetFilePath(name);
            if (!File.Exists(path)) yield break;

            var fileInfo = new FileInfo(path);
            long fileBytes = fileInfo.Length * 2; // UTF-16 approximation

            // If the file fits in the remaining cache budget, load it fully and filter from memory
            if (fileBytes <= _maxCacheBytes - _totalCachedBytes)
            {
                LoadAndCache(name);
                if (_cache.TryGetValue(name, out var fresh))
                {
                    foreach (var doc in fresh.Documents)
                    {
                        if (predicate(doc))
                            yield return doc;
                    }
                }
                yield break;
            }

            // File is too large for cache – streaming path: process line-by-line
            var matchedBatch = new List<Dictionary<string, object>>();
            long batchBytes = 0;

            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Dictionary<string, object>? fullDoc;
                try
                {
                    fullDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(line, _jsonOptions);
                }
                catch { continue; }

                if (fullDoc == null) continue;

                if (predicate(fullDoc))
                {
                    matchedBatch.Add(fullDoc);
                    // Rough estimate: each Dictionary<string,object> entry ~= 1KB overhead + data
                    batchBytes += 1024 + (line.Length * 2);
                }

                // Flush batch when approaching cache limit to prevent memory pressure
                if (batchBytes >= _maxCacheBytes / 2)
                {
                    foreach (var doc in matchedBatch)
                        yield return doc;
                    matchedBatch.Clear();
                    batchBytes = 0;
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }

            // Flush remaining
            foreach (var doc in matchedBatch)
                yield return doc;
        }

        private string GetFilePath(string name)
            => Path.Combine(_bulkPath, $"{name}.jsonl");

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
