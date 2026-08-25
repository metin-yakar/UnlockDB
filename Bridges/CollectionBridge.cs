using Jint;
using Jint.Native;
using AxarDB.Definitions;
using AxarDB.Wrappers;
using AxarDB.Core;

namespace AxarDB.Bridges
{
    public class CollectionBridge
    {
        private readonly DatabaseEngine _dbEngine;
        public readonly Collection _collection;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public CollectionBridge(DatabaseEngine dbEngine, Collection collection, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _dbEngine = dbEngine;
            _collection = collection;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        public void delete()
        {
            _dbEngine.DeleteCollection(_collection.Name);
        }

        public void reload()
        {
            Console.WriteLine($"[DB] Reloading collection: {_collection.Name}");
            _collection.Reload();
        }

        public ResultSet findall(Jint.Native.JsValue? predicate = null)
        {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                return new ResultSet(_collection.FindAll(), _collection);
            }
            
            // Analyze Predicate
            AxarDB.Query.QueryOptimizer.AnalysisResult? analysis = null;
            var optimized = AxarDB.Query.QueryOptimizer.AnalyzePredicate(predicate);
            if (optimized != null)
            {
                analysis = new AxarDB.Query.QueryOptimizer.AnalysisResult 
                { 
                    Prop = optimized.Value.prop!, 
                    Val = optimized.Value.val!, 
                    Op = optimized.Value.op 
                };
            }

            Func<Dictionary<string, object>, bool> csPredicate = (d) => 
            {
                 // We execute the JS function using the Engine.
                 // We need to pass the document as argument.
                 if (_engine == null) return true; // Fallback?

                 lock (_engine)
                 {
                     try
                     {
                        var result = _engine.Invoke(predicate, new object[] { new DocumentWrapper(d) });
                        return result.AsBoolean();
                     }
                     catch (Exception ex)
                     {
                        Console.WriteLine($"[DB Predicate Error] {ex.Message} | Stack: {ex.StackTrace}");
                        return false;
                     }
                 }
            };

            var results = _collection.FindAll(csPredicate, analysis, _cancellationToken);
            return new ResultSet(results, _collection);
        }

        public DocumentWrapper? find(Func<object, bool> predicate)
        {
             // Fix: Lock engine for thread safety during Parallel execution
             Func<Dictionary<string, object>, bool> safePredicate = (d) => 
             {
                 if (_engine == null) return predicate(new DocumentWrapper(d));
                 lock (_engine) 
                 {
                     try { return predicate(new DocumentWrapper(d)); } catch { return false; }
                 }
             };

             var doc = _collection.FindAll(safePredicate, null, _cancellationToken).FirstOrDefault();
             return doc != null ? new DocumentWrapper(doc) : null;
        }

        public void update(Jint.Native.JsValue predicate, object updateFields)
        {
             var resultSet = findall(predicate);
             resultSet.update(updateFields);
        }

        public AxarList select(Func<object, object> selector)
        {
             Func<Dictionary<string, object>, object> safeSelector = (d) => 
             {
                 if (_engine == null) return selector(new DocumentWrapper(d));
                 lock (_engine) { return selector(new DocumentWrapper(d)); }
             };

             var list = _collection.FindAll(_cancellationToken).Select(safeSelector);
             return new AxarList(list);
        }

        public object? insert(object docObj)
        {
            if (_collection.Name.StartsWith("sys", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(_collection.Name, "sysusers", StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.InvalidOperationException($"Direct insertion into system collection '{_collection.Name}' is not allowed.");
                }
            }

            var dict = ConvertToDictionary(docObj);
            if (dict != null)
            {
                _collection.Insert(dict, _cancellationToken);
                return dict;
            }
            return null;
        }
        
        public void index(Func<object, object> propertySelector, string type)
        {
             var tracer = new IndexTracer();
             try 
             {
                 propertySelector(tracer);
             } 
             catch { }
             
             if (!string.IsNullOrEmpty(tracer.TracedProperty))
             {
                 _collection.CreateIndex(tracer.TracedProperty, type);
             }
        }

        public void index(string propertyName, string type)
        {
            _collection.CreateIndex(propertyName, type);
        }

        public ResultSet contains(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) => 
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine) 
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var results = _collection.FindAll(safePredicate, null, _cancellationToken);
            return new ResultSet(results, _collection);
        }

        public ResultSet startsWith(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) => 
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine) 
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var results = _collection.FindAll(safePredicate, null, _cancellationToken);
            return new ResultSet(results, _collection);
        }

        public bool exists(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) => 
            {
                if (_engine == null) return predicate(new DocumentWrapper(d));
                lock (_engine) 
                {
                    try { return predicate(new DocumentWrapper(d)); } catch { return false; }
                }
            };
            return _collection.FindAll(safePredicate, null, _cancellationToken).Any();
        }

        private Dictionary<string, object>? ConvertToDictionary(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> dict) return dict;
            if (obj is IDictionary<string, object> idict) return new Dictionary<string, object>(idict);
            if (obj is System.Dynamic.ExpandoObject expando)
            {
                return expando.ToDictionary(k => k.Key, v => v.Value ?? new object());
            }
            return null;
        }
    }
}
