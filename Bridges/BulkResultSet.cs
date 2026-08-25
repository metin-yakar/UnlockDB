using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB.Bridges
{
    /// <summary>
    /// ResultSet for bulk (JSONL) collections. Supports chaining but no direct modification.
    /// delete() rewrites the JSONL file without the matched rows.
    ///
    /// Enumeration yields the underlying <see cref="Dictionary{string,object}"/> directly so
    /// Jint can marshal the result into a JavaScript array WITHOUT an extra `.toList()` call
    /// and WITHOUT a per-document <see cref="DocumentWrapper"/> allocation. Wrappers are used
    /// only where a script explicitly needs them (select/first/find/foreach predicates).
    /// </summary>
    public class BulkResultSet : IEnumerable<Dictionary<string, object>>
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly BulkStore _store;
        private readonly string _collectionName;

        public BulkResultSet(IEnumerable<Dictionary<string, object>> source, BulkStore store, string collectionName)
        {
            _source = source;
            _store = store;
            _collectionName = collectionName;
        }

        public IEnumerator<Dictionary<string, object>> GetEnumerator() => _source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public List<Dictionary<string, object>> toList() => _source.ToList();
        public List<Dictionary<string, object>> ToList() => toList();

        public BulkResultSet take(int count)
            => new BulkResultSet(_source.Take(count), _store, _collectionName);

        public BulkResultSet skip(int count)
            => new BulkResultSet(_source.Skip(count), _store, _collectionName);

        public AxarList select(Func<object, object> selector)
            => new AxarList(_source.Select(d => selector(new DocumentWrapper(d))));

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

        /// <summary>
        /// Removes all matched documents from the JSONL file (rewrites file without them).
        /// </summary>
        public void delete()
        {
            var ids = _source
                .Where(d => d.ContainsKey("_id"))
                .Select(d => d["_id"].ToString()!)
                .ToHashSet();

            _store.Delete(_collectionName, d =>
                d.TryGetValue("_id", out var id) && ids.Contains(id.ToString()!));
        }

        public void update(object updateFields)
        {
            if (updateFields == null) return;
            Dictionary<string, object>? fields = null;
            if (updateFields is Dictionary<string, object> d) fields = d;
            else if (updateFields is IDictionary<string, object> id) fields = new Dictionary<string, object>(id);
            else if (updateFields is System.Dynamic.ExpandoObject ex) fields = ex.ToDictionary(k => k.Key, v => v.Value ?? new object());
            
            if (fields == null) return;

            var ids = _source
                .Where(d => d.ContainsKey("_id"))
                .Select(d => d["_id"].ToString()!)
                .ToHashSet();

            if (ids.Count == 0) return;

            _store.Update(_collectionName, d =>
                d.TryGetValue("_id", out var id) && ids.Contains(id.ToString()!), fields);
        }

        public BulkResultSet orderBy(Func<object, object> selector)
        {
            var ordered = _source.OrderBy(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new BulkResultSet(ordered, _store, _collectionName);
        }

        public BulkResultSet orderByDesc(Func<object, object> selector)
        {
            var ordered = _source.OrderByDescending(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new BulkResultSet(ordered, _store, _collectionName);
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
