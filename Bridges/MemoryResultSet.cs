using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB.Bridges
{
    /// <summary>
    /// A ResultSet variant for in-memory collections.
    /// Supports chaining (take, skip, select, toList, foreach, count, delete).
    ///
    /// IMPORTANT: enumeration yields the underlying <see cref="Dictionary{string,object}"/>
    /// directly — NOT a <see cref="DocumentWrapper"/>. The documents are already plain CLR
    /// objects (converted via CustomObjectConverter), so returning them directly lets Jint
    /// marshal the result into a real JavaScript array WITHOUT an extra `.toList()` call and
    /// WITHOUT allocating a wrapper per document. <see cref="DocumentWrapper"/> is still used
    /// only where a script explicitly needs it (select/first/find/foreach predicates).
    /// </summary>
    public class MemoryResultSet : IEnumerable<Dictionary<string, object>>
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly MemoryStore _store;
        private readonly string _collectionName;

        public MemoryResultSet(IEnumerable<Dictionary<string, object>> source, MemoryStore store, string collectionName)
        {
            _source = source;
            _store = store;
            _collectionName = collectionName;
        }

        public IEnumerator<Dictionary<string, object>> GetEnumerator() => _source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public List<Dictionary<string, object>> toList() => _source.ToList();
        public List<Dictionary<string, object>> ToList() => toList();

        public MemoryResultSet take(int count)
            => new MemoryResultSet(_source.Take(count), _store, _collectionName);

        public MemoryResultSet skip(int count)
            => new MemoryResultSet(_source.Skip(count), _store, _collectionName);

        public AxarList select(Func<object, object> selector)
        {
            var list = _source.Select(d => selector(new DocumentWrapper(d)));
            return new AxarList(list);
        }

        public DocumentWrapper? first()
        {
            var doc = _source.FirstOrDefault();
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return _source.Count();
            return _source.Count(d => predicate(new DocumentWrapper(d)));
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
            foreach (var doc in _source)
                action(new DocumentWrapper(doc));
        }

        public AxarList distinct(Func<object, object>? selector = null)
        {
            if (selector == null) return new AxarList(_source.Select(d => (object)d).Distinct());
            return new AxarList(_source.Select(d => selector(new DocumentWrapper(d))).Distinct());
        }

        public void update(object updateFields)
        {
            if (updateFields == null) return;

            Dictionary<string, object>? fields = null;
            if (updateFields is Dictionary<string, object> d) fields = d;
            else if (updateFields is IDictionary<string, object> id) fields = new Dictionary<string, object>(id);
            else if (updateFields is System.Dynamic.ExpandoObject ex) fields = ex.ToDictionary(k => k.Key, v => v.Value ?? new object());

            if (fields == null) return;

            var docsToUpdate = _source.ToList();
            if (docsToUpdate.Count == 0)
            {
                throw new KeyNotFoundException("No documents found matching the update criteria.");
            }

            foreach (var doc in docsToUpdate)
            {
                string id = doc["_id"].ToString()!;
                foreach (var kv in fields)
                {
                    if (kv.Key == "_id") continue; // Prevent altering _id
                    doc[kv.Key] = kv.Value;
                }
                
                // For memory store, since we update the object directly in memory, 
                // we technically don't need a specific _store.Update() unless we need to reset TTL or indices.
                // However, doing it safely:
                _store.Update(_collectionName, id, doc);
            }
        }

        /// <summary>
        /// Deletes all documents in this result set from the MemoryStore.
        /// </summary>
        public void delete()
        {
            // Materialize IDs first to avoid modifying the collection while iterating
            var ids = _source
                .Where(d => d.ContainsKey("_id"))
                .Select(d => d["_id"].ToString()!)
                .ToList();

            _store.Delete(_collectionName, d =>
                d.TryGetValue("_id", out var id) && ids.Contains(id.ToString()!));
        }

        public MemoryResultSet orderBy(Func<object, object> selector)
        {
            var ordered = _source.OrderBy(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new MemoryResultSet(ordered, _store, _collectionName);
        }

        public MemoryResultSet orderByDesc(Func<object, object> selector)
        {
            var ordered = _source.OrderByDescending(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new MemoryResultSet(ordered, _store, _collectionName);
        }

        public object max(Func<object, object> selector)
        {
            var values = _source.Select(d => selector(new DocumentWrapper(d))).Where(v => v != null);
            object maxVal = null;
            var comparer = AxarDB.Helpers.UniversalComparer.Instance;
            foreach (var val in values)
            {
                if (maxVal == null || comparer.Compare(val, maxVal) > 0) maxVal = val;
            }
            return maxVal;
        }

        public object min(Func<object, object> selector)
        {
            var values = _source.Select(d => selector(new DocumentWrapper(d))).Where(v => v != null);
            object minVal = null;
            var comparer = AxarDB.Helpers.UniversalComparer.Instance;
            foreach (var val in values)
            {
                if (minVal == null || comparer.Compare(val, minVal) < 0) minVal = val;
            }
            return minVal;
        }
    }
}
