using System.Dynamic;
using AxarDB.Definitions;
using AxarDB.Core;

namespace AxarDB.Bridges
{
    public class AxarDBBridge : DynamicObject
    {
        private readonly DatabaseEngine _dbEngine;
        private readonly Jint.Engine _jintEngine;
        private readonly CancellationToken _cancellationToken;

        public AxarDBBridge(DatabaseEngine dbEngine, Jint.Engine jintEngine, CancellationToken cancellationToken = default)
        {
            _dbEngine = dbEngine;
            _jintEngine = jintEngine;
            _cancellationToken = cancellationToken;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            // Block access to custom sys-prefixed collections
            if (binder.Name.StartsWith("sys", StringComparison.OrdinalIgnoreCase) &&
                !binder.Name.Equals("sysusers", StringComparison.OrdinalIgnoreCase) &&
                !binder.Name.Equals("sysqueue", StringComparison.OrdinalIgnoreCase) &&
                !binder.Name.Equals("sysvaults", StringComparison.OrdinalIgnoreCase) &&
                !binder.Name.Equals("sysconfig", StringComparison.OrdinalIgnoreCase) &&
                !binder.Name.Equals("syslogs", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"System collection name '{binder.Name}' is reserved.");
            }

            if (binder.Name.Equals("syslogs", StringComparison.OrdinalIgnoreCase))
            {
                result = new LogCollectionBridge(_dbEngine.BasePath, "syslogs", _jintEngine, _cancellationToken);
                return true;
            }

            // db.users -> returns CollectionBridge for "users"
            var collection = _dbEngine.GetCollection(binder.Name);
            result = new CollectionBridge(_dbEngine, collection, _jintEngine, _cancellationToken);
            return true;
        }

        public void deleteCollection(string name)
        {
            _dbEngine.DeleteCollection(name);
        }

        public AliasWrapper alias(object source, string name) => new AliasWrapper(source, name);
        
        // Static join method simulation (instance method on db)
        // Refactored for j1, j2, ... indexing and multi-join support
        // Added support for AliasWrapper
        public JoinCollectionBridge join(params object[] sources)
        {
            if (sources == null || sources.Length == 0) 
                return new JoinCollectionBridge(new List<object>(), _jintEngine);

            // Normalize the first source
            string firstKey = "j1";
            object firstData = sources[0];
            if (sources[0] is AliasWrapper aw1)
            {
                firstKey = aw1.Name;
                firstData = aw1.Source;
            }

            IEnumerable<object> joined = GetIterable(firstData).Select(x => (object)new Dictionary<string, object> { { firstKey, x } });

            for (int i = 1; i < sources.Length; i++)
            {
                string key = "j" + (i + 1);
                object currentData = sources[i];
                
                if (sources[i] is AliasWrapper aw)
                {
                    key = aw.Name;
                    currentData = aw.Source;
                }

                IEnumerable<object> nextSourceData = GetIterable(currentData);
                
                // Cartesian Product for relational join support
                joined = from l in joined.Cast<Dictionary<string, object>>()
                         from r in nextSourceData
                         select (object)new Dictionary<string, object>(l) { { key, r } };
            }

            return new JoinCollectionBridge(joined, _jintEngine);
        }

        private IEnumerable<object> GetIterable(object source)
        {
            if (source is CollectionBridge cb)
            {
                return cb._collection.FindAll().Select(d => new Wrappers.DocumentWrapper(d));
            }
            if (source is System.Collections.IEnumerable enumerable)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(item);
                }
                return list;
            }
            return new List<object> { source };
        }

        public bool addVault(string key, object value)
        {
            return _dbEngine.AddVault(key, value);
        }

        // View Methods
        public object? view(string name, object? parameters = null)
        {
            // Convert parameters to Dictionary<string, object>
            // Jint usually passes JsObject, we need to convert.
            Dictionary<string, object>? paramsDict = null;
            if (parameters is System.Collections.Generic.IDictionary<string, object> dict)
            {
                paramsDict = new Dictionary<string, object>(dict);
            }
            else if (parameters is System.Dynamic.ExpandoObject exp)
            {
                paramsDict = exp.ToDictionary(k => k.Key, v => v.Value ?? new object());
            }
            // If parameters is null or incompatible, we pass null.
            
            return _dbEngine.ExecuteView(name, paramsDict, "internal", "system", _cancellationToken);
        }

        public void saveView(string name, string content) => _dbEngine.SaveView(name, content);
        public void deleteView(string name) => _dbEngine.DeleteView(name);
        public List<string> listViews() => _dbEngine.ListViews();
        public string? getView(string name) => _dbEngine.GetViewContent(name);

        // Trigger Methods
        public void saveTrigger(string name, string target, string content) => _dbEngine.SaveTrigger(name, target, content);
        public void deleteTrigger(string name) => _dbEngine.DeleteTrigger(name);
        public List<string> listTriggers() => _dbEngine.ListTriggers();
        public string? getTrigger(string name) => _dbEngine.GetTriggerContent(name);
    }
}
