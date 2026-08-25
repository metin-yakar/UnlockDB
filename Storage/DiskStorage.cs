using System.Collections.Concurrent;
using System.Text.Json;

namespace AxarDB.Storage
{
    public class DiskStorage
    {
        private readonly string _basePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public DiskStorage(string basePath)
        {
            _basePath = basePath;
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
            
            _jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            _jsonOptions.Converters.Add(new CustomObjectConverter());
        }

        /// <summary>
        /// Absolute root of the data store (already includes the "Data" segment, e.g.
        /// bin/Release/net8.0/Data). Index files must be persisted under
        /// Path.Combine(BasePath, collectionName) so they live next to the collection's
        /// documents — using a relative "Data" path instead writes them to the wrong
        /// (working-directory) location and breaks persistence.
        /// </summary>
        public string BasePath => _basePath;

        public void EnsureCollection(string collectionName)
        {
            var path = Path.Combine(_basePath, collectionName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void SaveDocument(string collectionName, Dictionary<string, object> document)
        {
            if (!document.ContainsKey("_id")) return;
            string id = document["_id"].ToString()!;
            
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            // Write: Share Read to allow others to read while we write (if OS permits atomic swap or short lock)
            // But usually Write requires exclusive or Share.Read. 
            // We'll use Share.Read to be friendly to readers.
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(fs, document, _jsonOptions);
        }

        public Dictionary<string, object>? LoadDocument(string collectionName, string id)
        {
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            if (!File.Exists(path)) return null;

            try 
            {
                // Read: Share ReadWrite to allow writers to write while we read
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(fs, _jsonOptions);
            }
            catch (IOException) { return null; } // File locked or busy
            catch (JsonException) { return null; }
        }

        public void DeleteDocument(string collectionName, string id)
        {
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* Ignore if locked, maybe retry? */ }
            }
        }

        // Lazy loading of documents with optimized streaming
        public IEnumerable<Dictionary<string, object>> StreamDocuments(string collectionName)
        {
            var path = Path.Combine(_basePath, collectionName);
            if (!Directory.Exists(path)) yield break;

            foreach (var file in Directory.EnumerateFiles(path, "*.json"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("idx_")) continue;

                Dictionary<string, object>? doc = null;
                try 
                {
                    // Stream Read
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    doc = JsonSerializer.Deserialize<Dictionary<string, object>>(fs, _jsonOptions);
                }
                catch { /* Skip generic errors/locks for resilience during stream */ }

                if (doc != null) yield return doc;
            }
        }
        
        public IEnumerable<string> GetAllDocumentIds(string collectionName)
        {
             var path = Path.Combine(_basePath, collectionName);
             if (!Directory.Exists(path)) yield break;
             
             foreach(var file in Directory.EnumerateFiles(path, "*.json"))
             {
                 if (Path.GetFileName(file).StartsWith("idx_")) continue;
                 yield return Path.GetFileNameWithoutExtension(file);
             }
        }
    }

    public class CustomObjectConverter : System.Text.Json.Serialization.JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long l))
                        return l;
                    return reader.GetDouble();
                case JsonTokenType.String:
                    if (reader.TryGetDateTime(out DateTime date))
                        return date;
                    return reader.GetString()!;
                case JsonTokenType.StartObject:
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options)!;
                case JsonTokenType.StartArray:
                    return JsonSerializer.Deserialize<List<object>>(ref reader, options)!;
                default:
                    return JsonDocument.ParseValue(ref reader).RootElement.Clone();
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
