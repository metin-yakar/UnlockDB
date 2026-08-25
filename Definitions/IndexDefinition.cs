using Jint;
using Jint.Native;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AxarDB.Definitions
{
    public class IndexDefinition
    {
        public string PropertyName { get; set; }
        public string Type { get; set; } // "ASC" or "DESC"

        // Value -> List of IDs
        public ConcurrentDictionary<string, List<string>> TextIndex { get; set; } = new();
        public SortedDictionary<double, List<string>> NumericIndex { get; set; } = new(); // Better for range queries

        public IndexDefinition(string propertyName, string type)
        {
            PropertyName = propertyName;
            Type = type;
        }

        public void IndexDocument(Dictionary<string, object> document)
        {
            if (!document.TryGetValue(PropertyName, out var val) || val == null || !document.ContainsKey("_id")) return;
            string id = document["_id"].ToString()!;

            // Handle JsonElement from System.Text.Json
            if (val is JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.String)
                    val = el.GetString();
                else if (el.ValueKind == JsonValueKind.Number)
                    val = el.GetDouble();
            }

            // String Index
            if (val is string strVal)
            {
                var lowerVal = strVal.ToLowerInvariant(); // Exact match index (case-insensitive default for this DB)
                
                TextIndex.AddOrUpdate(lowerVal, 
                    new List<string> { id },
                    (k, list) => { lock(list) { if(!list.Contains(id)) list.Add(id); } return list; });
            }
            // Numeric Index
            else if (IsNumeric(val))
            {
                double num = Convert.ToDouble(val);
                lock(NumericIndex)
                {
                    if (!NumericIndex.ContainsKey(num))
                        NumericIndex[num] = new List<string>();
                    
                    if (!NumericIndex[num].Contains(id))
                        NumericIndex[num].Add(id);
                }
            }
        }
        
        public IEnumerable<string> Search(object value, string op)
        {
             if (value is string strVal)
             {
                 var k = strVal.ToLowerInvariant();
                 if (op == "==" || op == "===")
                 {
                     if (TextIndex.TryGetValue(k, out var list)) return list;
                 }
             }
             else if (IsNumeric(value))
             {
                 double num = Convert.ToDouble(value);
                 lock(NumericIndex) 
                 {
                     if (op == "==" || op == "===")
                     {
                         if (NumericIndex.TryGetValue(num, out var list)) return list;
                     }
                     else if (op == ">")
                     {
                         return NumericIndex.Where(x => x.Key > num).SelectMany(x => x.Value).ToList();
                     }
                     else if (op == ">=")
                     {
                         return NumericIndex.Where(x => x.Key >= num).SelectMany(x => x.Value).ToList();
                     }
                     else if (op == "<")
                     {
                         return NumericIndex.Where(x => x.Key < num).SelectMany(x => x.Value).ToList();
                     }
                     else if (op == "<=")
                     {
                         return NumericIndex.Where(x => x.Key <= num).SelectMany(x => x.Value).ToList();
                     }
                 }
             }
             return Enumerable.Empty<string>();
        }

        public void RemoveDocument(string id, object? value)
        {
            if (value is string strVal)
            {
                var k = strVal.ToLowerInvariant();
                if (TextIndex.TryGetValue(k, out var list))
                {
                    lock (list) { list.Remove(id); }
                }
            }
            else if (IsNumeric(value))
            {
                double num = Convert.ToDouble(value);
                lock (NumericIndex)
                {
                    if (NumericIndex.TryGetValue(num, out var list))
                        list.Remove(id);
                }
            }
        }

        private bool IsNumeric(object? expression)
        {
            if (expression == null)
                return false;

            return Double.TryParse(Convert.ToString(expression, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out double _);
        }
        
        // Persistence
        public void Save(string basePath)
        {
             var path = Path.Combine(basePath, $"idx_{PropertyName}.json");
             // Serialize: Num index keys must be strings for JSON
             var numDict = NumericIndex.ToDictionary(k => k.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), v => v.Value);
             var data = new { Prop = PropertyName, Type = Type, Txt = TextIndex, Num = numDict };
             File.WriteAllText(path, JsonSerializer.Serialize(data), System.Text.Encoding.UTF8);
        }

        public static IndexDefinition? Load(string path)
        {
             if(!File.Exists(path)) return null;
             try 
             {
                 var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                 // Deserialize to dynamic or element to handle manual parsing
                 using var doc = JsonDocument.Parse(json);
                 var root = doc.RootElement;
                 
                 var prop = root.GetProperty("Prop").GetString()!;
                 var type = root.TryGetProperty("Type", out var t) ? t.GetString()! : "ASC";
                 
                 var idx = new IndexDefinition(prop, type);
                 
                 if (root.TryGetProperty("Txt", out var txt))
                 {
                     foreach(var p in txt.EnumerateObject())
                     {
                         var list = JsonSerializer.Deserialize<List<string>>(p.Value.GetRawText());
                         if(list != null) idx.TextIndex[p.Name] = list;
                     }
                 }
                 
                 if (root.TryGetProperty("Num", out var num))
                 {
                     foreach(var p in num.EnumerateObject())
                     {
                         if(double.TryParse(p.Name, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double key))
                         {
                             var list = JsonSerializer.Deserialize<List<string>>(p.Value.GetRawText());
                             if(list != null) idx.NumericIndex[key] = list;
                         }
                     }
                 }
                 
                 return idx;
             }
             catch { return null; }
        }
    }

    // Wrapper for Descending Sort in Linq if needed (though we handle it in list sort above)
    public class DescComparable : IComparable
    {
        private readonly object _value;
        public DescComparable(object value) { _value = value; }
        
        public int CompareTo(object? obj)
        {
             if (obj is DescComparable other)
             {
                 return -1 * Comparer<object>.Default.Compare(_value, other._value);
             }
             return 0;
        }
    }
}
