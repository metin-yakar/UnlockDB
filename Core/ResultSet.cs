using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB.Core
{
    /// <summary>
    /// A ResultSet for persistent (disk-backed) collections.
    ///
    /// Enumeration yields the underlying <see cref="Dictionary{string,object}"/> directly so
    /// Jint can marshal the result into a JavaScript array WITHOUT an extra `.toList()` call
    /// and WITHOUT a per-document <see cref="DocumentWrapper"/> allocation. Wrappers are used
    /// only where a script explicitly needs them (select/first/find/foreach predicates, update).
    /// </summary>
    public class ResultSet : IEnumerable<Dictionary<string, object>>
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly AxarDB.Definitions.Collection? _collection;

        public ResultSet(IEnumerable<Dictionary<string, object>> source, AxarDB.Definitions.Collection? collection = null)
        {
            _source = source;
            _collection = collection;
        }

        private ResultSet(IEnumerable<DocumentWrapper> source, AxarDB.Definitions.Collection? collection = null)
        {
            _source = source.Select(w => w.Data);
            _collection = collection;
        }

        public IEnumerator<Dictionary<string, object>> GetEnumerator() => _source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public List<Dictionary<string, object>> ToList() => _source.ToList();
        public List<Dictionary<string, object>> toList() => _source.ToList();

        public DocumentWrapper? first()
        {
            var doc = _source.FirstOrDefault();
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public ResultSet take(int count)
        {
            var taken = _source.Take(count);
            return new ResultSet(taken, _collection);
        }

        public ResultSet skip(int count)
        {
            var skipped = _source.Skip(count);
            return new ResultSet(skipped, _collection);
        }

        public AxarList select(Func<object, object> selector)
        {
            var list = _source.Select(d => selector(new DocumentWrapper(d)));
            return new AxarList(list);
        }

        public void update(object updateFields)
        {
            if (_collection == null || updateFields == null) return;

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
                var oldDocCopy = AxarDB.Helpers.ScriptUtils.DeepCopy(doc) as Dictionary<string, object>;
                string id = doc["_id"].ToString()!;

                foreach (var kv in fields)
                {
                    if (kv.Key == "_id") continue; // Prevent altering _id
                    doc[kv.Key] = kv.Value;
                }
                doc["_id"] = id; // Enforce original _id

                // bypassSystemRules=true: update reuses UpdateExisting internally;
                // it must bypass insert/update restrictions on system collections (e.g. sysconfig).
                _collection.UpdateExisting(doc, oldDocCopy!, bypassSystemRules: true);
            }
            _collection.SaveIndices();
        }

        public void delete()
        {
            if (_collection == null) return;

            var idsToDelete = _source
                .Select(d => d.TryGetValue("_id", out var id) ? id.ToString() : null)
                .Where(x => x != null)
                .Cast<string>()
                .ToList();

            if (idsToDelete.Count == 0) return;

            _collection.Delete(d => d.TryGetValue("_id", out var id) && idsToDelete.Contains(id.ToString()!));

            // _source in delete can't be cleared since it's an IEnumerable now.
            // The user must not iterate a ResultSet after deleting it.
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
            foreach (var item in _source) action(new DocumentWrapper(item));
        }

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return _source.Count();
            return _source.Count(d => predicate(new DocumentWrapper(d)));
        }

        public AxarList distinct(Func<object, object>? selector = null)
        {
            if (selector == null) return new AxarList(_source.Select(d => (object)d).Distinct());
            return new AxarList(_source.Select(d => selector(new DocumentWrapper(d))).Distinct());
        }

        public ResultSet orderBy(Func<object, object> selector)
        {
            var ordered = _source.OrderBy(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new ResultSet(ordered, _collection);
        }

        public ResultSet orderByDesc(Func<object, object> selector)
        {
            var ordered = _source.OrderByDescending(d => selector(new DocumentWrapper(d)), AxarDB.Helpers.UniversalComparer.Instance);
            return new ResultSet(ordered, _collection);
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
