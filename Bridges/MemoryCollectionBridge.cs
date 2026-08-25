using Jint;
using Jint.Native;
using AxarDB.Wrappers;
using System.Diagnostics;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Bridge for a single in-memory collection.
    /// Exposes insert, findall, find, and delete — matching CollectionBridge's API.
    /// </summary>
    public class MemoryCollectionBridge
    {
        private static readonly bool Diag = Environment.GetEnvironmentVariable("AXARDB_DIAG") == "1";
        private static void Log(string step, string detail, long ms)
        {
            if (Diag) Console.WriteLine($"[diag:memory-bridge] {step} | {detail} | {ms} ms");
        }

        private readonly MemoryStore _store;
        private readonly string _collectionName;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public MemoryCollectionBridge(MemoryStore store, string collectionName, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _store = store;
            _collectionName = collectionName;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Insert a document (or a batch of documents) into the in-memory collection.
        /// Accepts either a single JavaScript object or an array/list of objects;
        /// arrays are inserted in a single store call to avoid per-document bridge overhead.
        /// </summary>
        /// <param name="docsObj">JavaScript object, or array/list of objects, to insert.</param>
        /// <param name="hours">TTL in hours. Defaults to 1 hour if not provided.</param>
        public object? insert(object docsObj, double hours = 1.0)
        {
            if (docsObj is IEnumerable<object> enumerable && !(docsObj is string))
            {
                var list = new List<Dictionary<string, object>>();
                var skipped = 0;
                foreach (var item in enumerable)
                {
                    var dict = ConvertToDictionary(item);
                    if (dict != null) list.Add(dict);
                    else skipped++;
                }
                if (skipped > 0) Log("insert", $"SKIPPED {skipped} docs (could not convert to Dictionary)", 0);
                if (list.Count == 0) return 0;
                var swBatch = Stopwatch.StartNew();
                var resBatch = _store.Insert(_collectionName, list, hours);
                swBatch.Stop();
                Log("insert(batch)", $"{_collectionName} docs={list.Count}", swBatch.ElapsedMilliseconds);
                return resBatch;
            }

            var single = ConvertToDictionary(docsObj);
            if (single == null) return null;

            var swSingle = Stopwatch.StartNew();
            var resSingle = _store.Insert(_collectionName, single, hours);
            swSingle.Stop();
            Log("insert(single)", _collectionName, swSingle.ElapsedMilliseconds);
            return resSingle;
        }

        /// <summary>
        /// Returns a MemoryResultSet of all non-expired documents, optionally filtered by a predicate.
        /// </summary>
        public MemoryResultSet findall(JsValue? predicate = null)
        {
            var sw = Stopwatch.StartNew();
            var all = _store.FindAll(_collectionName);
            var storeMs = sw.ElapsedMilliseconds;

            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                Log("findall(all)", $"{_collectionName} docs={all.Count()}", storeMs);
                return new MemoryResultSet(all, _store, _collectionName);
            }

            // Optimize simple queries
            var optimized = AxarDB.Query.QueryOptimizer.AnalyzePredicate(predicate);
            if (optimized != null)
            {
                var analysis = new AxarDB.Query.QueryOptimizer.AnalysisResult
                {
                    Prop = optimized.Value.prop!,
                    Val = optimized.Value.val!,
                    Op = optimized.Value.op
                };

                var filtered = all.Where(d => AxarDB.Query.QueryOptimizer.Evaluate(d, analysis)).ToList();
                Log("findall(fast)", $"{_collectionName} prop={analysis.Prop} op={analysis.Op} matched={filtered.Count}", storeMs);
                return new MemoryResultSet(filtered, _store, _collectionName);
            }

            Log("findall(SLOW-js)", $"{_collectionName} predicate='{predicate.ToString().Replace("\n", " ")}' docs={all.Count()}", storeMs);
            Func<Dictionary<string, object>, bool> csPredicate = (d) =>
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try
                    {
                        var result = _engine.Invoke(predicate, new object[] { new DocumentWrapper(d) });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            };

            return new MemoryResultSet(all.Where(csPredicate).ToList(), _store, _collectionName);
        }

        /// <summary>
        /// Returns the count of all active documents in this collection.
        /// </summary>
        public int count()
        {
            return _store.GetRecordCount(_collectionName);
        }

        /// <summary>
        /// Returns the first non-expired document matching the predicate, or null if not found.
        /// </summary>
        public DocumentWrapper? find(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new DocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new DocumentWrapper(d)); } catch { return false; }
                }
            };

            var doc = _store.FindAll(_collectionName).FirstOrDefault(safePredicate);
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public void update(JsValue predicate, object updateFields)
        {
            var resultSet = findall(predicate);
            resultSet.update(updateFields);
        }

        public MemoryResultSet contains(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var all = _store.FindAll(_collectionName);
            return new MemoryResultSet(all.Where(safePredicate), _store, _collectionName);
        }

        public MemoryResultSet startsWith(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var all = _store.FindAll(_collectionName);
            return new MemoryResultSet(all.Where(safePredicate), _store, _collectionName);
        }

        /// <summary>
        /// Deletes documents matching the predicate from the in-memory collection.
        /// When called without arguments, drops the entire collection from the store.
        /// </summary>
        public void delete(JsValue? predicate = null)
        {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                // Drop the whole collection so it disappears from memory/list
                _store.DropCollection(_collectionName);
                return;
            }

            Func<Dictionary<string, object>, bool> csPredicate = (d) =>
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try
                    {
                        var result = _engine.Invoke(predicate, new object[] { new DocumentWrapper(d) });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            };

            _store.Delete(_collectionName, csPredicate);
        }

        private static Dictionary<string, object>? ConvertToDictionary(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> dict) return dict;
            if (obj is IDictionary<string, object> idict) return new Dictionary<string, object>(idict);
            if (obj is System.Dynamic.ExpandoObject expando)
                return expando.ToDictionary(k => k.Key, v => v.Value ?? new object());
            return null;
        }
    }
}
