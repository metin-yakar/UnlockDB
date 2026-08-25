using Jint;
using Jint.Native;
using AxarDB.Wrappers;
using System.Diagnostics;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Bridge for a single JSONL bulk collection.
    /// Supports: insert([]), findall(predicate?), find(predicate), reload(), delete()
    /// </summary>
    public class BulkCollectionBridge
    {
        private readonly BulkStore _store;
        private readonly string _collectionName;
        private readonly Engine? _engine;

        private static readonly bool Diag = Environment.GetEnvironmentVariable("AXARDB_DIAG") == "1";
        private static void Log(string step, string detail, long ms)
        {
            if (Diag) Console.WriteLine($"[diag:bulk-bridge] {step} | {detail} | {ms} ms");
        }

        public BulkCollectionBridge(BulkStore store, string collectionName, Engine? engine = null)
        {
            _store = store;
            _collectionName = collectionName;
            _engine = engine;
        }

        /// <summary>
        /// Insert an array of documents into the JSONL collection.
        /// Creates the file if it doesn't exist.
        /// </summary>
        public object insert(object docsObj)
        {
            var list = ConvertToList(docsObj);
            if (list == null || list.Count == 0) return 0;
            var sw = Stopwatch.StartNew();
            _store.Insert(_collectionName, list);
            sw.Stop();
            Log("insert", $"{_collectionName} docs={list.Count}", sw.ElapsedMilliseconds);
            return list.Count;
        }

        /// <summary>Returns a BulkResultSet of all documents, optionally filtered.</summary>
        public BulkResultSet findall(JsValue? predicate = null)
        {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                var all = _store.GetDocuments(_collectionName);
                return new BulkResultSet(all, _store, _collectionName);
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

                var sw = Stopwatch.StartNew();
                var all = _store.GetDocuments(_collectionName);
                var filtered = all.Where(d => AxarDB.Query.QueryOptimizer.Evaluate(d, analysis)).ToList();
                sw.Stop();
                Log("findall(fast)", $"{_collectionName} prop={analysis.Prop} op={analysis.Op} matched={filtered.Count}", sw.ElapsedMilliseconds);
                return new BulkResultSet(filtered, _store, _collectionName);
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

            var filterFields = AxarDB.Query.QueryOptimizer.ExtractAccessedProperties(predicate);
            var results = _store.QueryChunks(_collectionName, filterFields, csPredicate);
            return new BulkResultSet(results, _store, _collectionName);
        }

        /// <summary>Returns the first document matching the predicate, or null.</summary>
        public DocumentWrapper? find(JsValue predicate)
        {
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

            var filterFields = AxarDB.Query.QueryOptimizer.ExtractAccessedProperties(predicate);
            var doc = _store.QueryChunks(_collectionName, filterFields, csPredicate).FirstOrDefault();
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public void update(JsValue predicate, object updateFields)
        {
            var resultSet = findall(predicate);
            resultSet.update(updateFields);
        }

        public BulkResultSet contains(JsValue predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try
                    {
                        var result = _engine.Invoke(predicate, new object[] { new CaseInsensitiveDocumentWrapper(d) });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            };

            var filterFields = AxarDB.Query.QueryOptimizer.ExtractAccessedProperties(predicate);
            var results = _store.QueryChunks(_collectionName, filterFields, safePredicate);
            return new BulkResultSet(results, _store, _collectionName);
        }

        public BulkResultSet startsWith(JsValue predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try
                    {
                        var result = _engine.Invoke(predicate, new object[] { new CaseInsensitiveDocumentWrapper(d) });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            };

            var filterFields = AxarDB.Query.QueryOptimizer.ExtractAccessedProperties(predicate);
            var results = _store.QueryChunks(_collectionName, filterFields, safePredicate);
            return new BulkResultSet(results, _store, _collectionName);
        }

        /// <summary>Manually reload this collection from disk.</summary>
        public void reload() => _store.Reload(_collectionName);

        /// <summary>
        /// Deletes all documents from this bulk collection by removing its JSONL file.
        /// After this call the collection will no longer appear in the sidebar.
        /// </summary>
        public void delete() => _store.DropCollection(_collectionName);

        /// <summary>Count of all documents in this collection.</summary>
        public int count() => _store.GetDocuments(_collectionName).Count();

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static List<Dictionary<string, object>>? ConvertToList(object? obj)
        {
            if (obj == null) return null;

            // Jint array/list
            if (obj is IEnumerable<object> enumerable)
            {
                var result = new List<Dictionary<string, object>>();
                foreach (var item in enumerable)
                {
                    var dict = ConvertSingle(item);
                    if (dict != null) result.Add(dict);
                }
                return result;
            }

            // Single document wrapped in list
            var single = ConvertSingle(obj);
            return single != null ? new List<Dictionary<string, object>> { single } : null;
        }

        private static Dictionary<string, object>? ConvertSingle(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> d) return d;
            if (obj is IDictionary<string, object> id) return new Dictionary<string, object>(id);
            if (obj is System.Dynamic.ExpandoObject exp) return exp.ToDictionary(k => k.Key, v => v.Value ?? new object());
            return null;
        }
    }
}
