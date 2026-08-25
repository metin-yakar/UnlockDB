using Jint;
using System.Collections.Concurrent;
using AxarDB.Bridges;
using AxarDB.Definitions;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MySqlConnector;
using Npgsql;
using System.Text;

namespace AxarDB.Core
{
    public class DatabaseEngine
    {
        private ConcurrentDictionary<string, Collection> _collections = new();
        private readonly AxarDB.Storage.DiskStorage _storage;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _sharedCache;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _basePath;
        private readonly AxarDB.Bridges.MemoryStore _memoryStore = new();
        private readonly AxarDB.Bridges.BulkStore _bulkStore;

        public AxarDB.Bridges.BulkStore BulkStore => _bulkStore;
        public AxarDB.Bridges.MemoryStore MemoryStore => _memoryStore;
        public string BasePath => _basePath;

        private string FormatLog(object o)
        {
            if (o == null) return "null";
            if (o is string s) return s;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.None, // Compact JSON as requested
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = new List<JsonConverter> 
                    { 
                        new Newtonsoft.Json.Converters.ExpandoObjectConverter(),
                        new Newtonsoft.Json.Converters.StringEnumConverter()
                    }
                };
                return JsonConvert.SerializeObject(o, settings);
            }
            catch
            {
                return o?.ToString() ?? "null";
            }
        }

        private object PerformHttpRequest(string method, string url, object? data, object? headers)
        {
            try 
            {
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), url);

                if (data != null && (method == "POST" || method == "PUT"))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                }

                // Handle Headers
                if (headers != null)
                {
                    if (headers is IDictionary<string, object> headerDict)
                    {
                        foreach (var kvp in headerDict)
                        {
                            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value?.ToString());
                        }
                    }
                    else if (headers is System.Collections.IEnumerable headerList)
                    {
                        foreach (var item in headerList)
                        {
                             if (item is IDictionary<string, object> dict)
                             {
                                 foreach (var kvp in dict) 
                                     request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value?.ToString());
                             }
                        }
                    }
                }

                var response = _httpClient.SendAsync(request).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;
                
                object? responseData;
                try {
                     responseData = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(responseString);
                } catch {
                     responseData = responseString;
                }

                return new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode, data = responseData };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }


        private void LogMySqlRequest(ScriptContext context, string connectionString, string query, object? parameters, long durationMs, bool success, string? error = null)
        {
             try 
             {
                 var logEntry = new 
                 {
                     timestamp = ServerTime.Now,
                     ip = context.IpAddress,
                     user = context.User,
                     type = context.IsView ? "view_mysql" : "script_mysql",
                     view = context.IsView ? context.ViewName : null,
                     connection = connectionString, // Maybe hide password? User didn't specify. Assuming raw for now as per "standard".
                     query,
                     parameters,
                     durationMs,
                     success,
                     error
                 };
                 
                 var json = System.Text.Json.JsonSerializer.Serialize(logEntry);
                 var path = Path.Combine(_basePath, "request_logs", $"{ServerTime.Now:yyyy-MM-dd}_mysql.log"); // Separate or same? User said "standard log types".
                 
                 //FOR AI TESTING PURPOSES
                 // Actually Logger.LogRequest format is specific pipe-delimited. 
                 // User requested "request_log, error_log... standard log types".
                 // Let's stick to existing Logger class structure OR extend it.
                 // Given the complexity of SQL logs, JSON might be better but let's try to fit into Logger.
                 // However, "request_log" usually implies HTTP requests in this project. 
                 // The prompt specified that request_log, error_log, etc. should be written according to the standard format of the relevant log type.
                 // I will use a new method in Logger or just append to a dedicated mysql log file to avoid polluting HTTP logs 
                 // OR I simply use Logger.LogRequest if it fits? 
                 // Logger.LogRequest takes (ip, user, json, duration, success).
                 
                 AxarDB.Logging.Logger.LogRequest(context.IpAddress, context.User, $"[MySQL] {query}", durationMs, success, error ?? "");
                 
                 
                 if (!success && !string.IsNullOrEmpty(error))
                 {
                     AxarDB.Logging.Logger.LogError($"[MySQL Error] {error} | Query: {query}");
                 }
             }
             catch {}
        }

        private object MySqlRead(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try 
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                if (parameters != null)
                {
                     var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                     var dict = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(json) as System.Collections.Generic.IDictionary<string, object>;
                     if (dict != null)
                     {
                         foreach(var kvp in dict)
                         {
                             command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);
                         }
                     }
                }
                
                using var reader = command.ExecuteReader();
                var results = new List<Dictionary<string, object>>();
                
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i);
                        if (val == DBNull.Value) val = null;
                        row[reader.GetName(i)] = val!;
                    }
                    results.Add(row);
                }
                
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, ex.Message);
                throw new Exception($"MySQL Exec Failed: {ex.Message}");
            }
        }

        private int MySqlExec(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try 
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                if (parameters != null)
                {
                     var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                     var dict = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(json) as System.Collections.Generic.IDictionary<string, object>;
                     if (dict != null)
                     {
                         foreach(var kvp in dict)
                         {
                             command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);
                         }
                     }
                }
                
                int affected = command.ExecuteNonQuery();
                
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return affected;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, ex.Message);
                throw new Exception($"MySQL Exec Failed: {ex.Message}");
            }
        }

        private void RegisterUtils(Engine engine, ScriptContext context)
        {
            // --- Utility Functions ---
            engine.SetValue("md5", new Func<string, string>(AxarDB.Helpers.ScriptUtils.MD5));
            engine.SetValue("sha256", new Func<string, string>(AxarDB.Helpers.ScriptUtils.SHA256));
            engine.SetValue("toString", new Func<object, string>(AxarDB.Helpers.ScriptUtils.ToString));
            engine.SetValue("randomNumber", new Func<int, int, int>(AxarDB.Helpers.ScriptUtils.RandomNumber));
            engine.SetValue("randomDecimal", new Func<string, string, decimal>(AxarDB.Helpers.ScriptUtils.RandomDecimal));
            engine.SetValue("randomString", new Func<int, string>(AxarDB.Helpers.ScriptUtils.RandomString));
            engine.SetValue("toBase64", new Func<string, string>(AxarDB.Helpers.ScriptUtils.ToBase64));
            engine.SetValue("fromBase64", new Func<string, string>(AxarDB.Helpers.ScriptUtils.FromBase64));
            engine.SetValue("encrypt", new Func<string, string, string>(AxarDB.Helpers.ScriptUtils.Encrypt));
            engine.SetValue("decrypt", new Func<string, string, string>(AxarDB.Helpers.ScriptUtils.Decrypt));
            engine.SetValue("split", new Func<string, string, string[]>(AxarDB.Helpers.ScriptUtils.Split));
            engine.SetValue("toDecimal", new Func<string, decimal>(AxarDB.Helpers.ScriptUtils.ToDecimal));
            engine.SetValue("guid", new Func<string>(() => AxarDB.Helpers.GuidV7.NewGuid().ToString()));

            // --- UUID v7 Functions ---
            // guidv7()             → new UUID v7 using the current UTC time
            // guidv7(datetime)     → new UUID v7 using the supplied datetime string (ISO 8601)
            // guidv7CreatedAt(str) → extracts the creation timestamp (UTC) from a UUID v7 string
            engine.SetValue("guidv7", new Func<object?, string>(arg =>
            {
                if (arg == null || arg is Jint.Native.JsValue jsv && jsv.IsNull() || arg is Jint.Native.JsValue jsv2 && jsv2.IsUndefined())
                    return AxarDB.Helpers.GuidV7.NewGuid().ToString();

                var argStr = arg.ToString();
                if (string.IsNullOrWhiteSpace(argStr))
                    return AxarDB.Helpers.GuidV7.NewGuid().ToString();

                if (DateTimeOffset.TryParse(argStr, null,
                        System.Globalization.DateTimeStyles.AssumeUniversal |
                        System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var dto))
                    return AxarDB.Helpers.GuidV7.NewGuid(dto).ToString();

                throw new InvalidOperationException(
                    $"guidv7(): Invalid datetime format '{argStr}'. Use ISO 8601, e.g. '2024-01-15T10:30:00Z'.");
            }));

            engine.SetValue("guidv7CreatedAt", new Func<string, object?>(guidStr =>
            {
                if (string.IsNullOrWhiteSpace(guidStr))
                    return null;
                var ts = AxarDB.Helpers.GuidV7.GetTimestamp(guidStr);
                return ts.HasValue ? (object)ts.Value.UtcDateTime : null;
            }));
            engine.SetValue("toJson", new Func<object, string>(o => System.Text.Json.JsonSerializer.Serialize(o, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));
            engine.SetValue("csv", new Func<object, object>(AxarDB.Helpers.ScriptUtils.Csv));

            // addSysUser(username, password) — safely add a user to sysusers
            engine.SetValue("addSysUser", new Func<string, string, object>((username, password) =>
            {
                if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("username cannot be empty.");
                if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("password cannot be empty.");
                var col = GetCollection("sysusers");
                // Check duplicate
                if (col.FindAll(d => d.TryGetValue("username", out var u) && string.Equals(u?.ToString(), username, StringComparison.OrdinalIgnoreCase)).Any())
                    throw new InvalidOperationException($"User '{username}' already exists.");
                var hashed = AxarDB.Helpers.ScriptUtils.SHA256(password);
                col.Insert(new Dictionary<string, object> { { "username", username }, { "password", hashed } }, bypassSystemRules: true);
                return new { username, status = "created" };
            }));

            // deleteSysUser(username) — safely remove a user from sysusers
            engine.SetValue("deleteSysUser", new Func<string, object>((username) =>
            {
                if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("username cannot be empty.");
                if (username.Equals("unlocker", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The default 'unlocker' user cannot be deleted.");
                var col = GetCollection("sysusers");
                col.Delete(d => d.TryGetValue("username", out var u) && string.Equals(u?.ToString(), username, StringComparison.OrdinalIgnoreCase));
                return new { username, status = "deleted" };
            }));
            
            // Join Alias Utility
            engine.SetValue("alias", new Func<object, string, AliasWrapper>((source, name) => new AliasWrapper(source, name)));

            // Deep Copy Utility
            engine.SetValue("deepcopy", new Func<object?, object?>(AxarDB.Helpers.ScriptUtils.DeepCopy));

            // MySQL Functions
            engine.SetValue("mysqlRead", new Func<string, string, object?, object>((conn, query, param) => 
                MySqlRead(conn, query, param, context)));
                
            engine.SetValue("mysqlExec", new Func<int>(() => 0)); // Placeholder override below
            
            engine.SetValue("mysqlExec", new Func<string, string, object?, int>((conn, query, param) => 
                MySqlExec(conn, query, param, context)));

            // PostgreSQL Functions
            engine.SetValue("pgsqlRead", new Func<string, string, object?, object>((conn, query, param) => 
                PgSqlRead(conn, query, param, context)));
                
            engine.SetValue("pgsqlExec", new Func<string, string, object?, int>((conn, query, param) => 
                PgSqlExec(conn, query, param, context)));


            // Webhook Function (POST)
            engine.SetValue("webhook", new Func<string, object, object?, object>((url, data, headers) => 
                PerformHttpRequest("POST", url, data, headers)));

            // HTTP Get Function
            engine.SetValue("httpGet", new Func<string, object?, object>((url, headers) => 
                PerformHttpRequest("GET", url, null, headers)));

            // OpenAI / LLM Function
            engine.SetValue("openai", new Func<string, string, AxarDB.Helpers.LlmClient>((url, token) => 
                new AxarDB.Helpers.LlmClient(url, token)));

            // Queue Function
            engine.SetValue("queue", new Func<string, object?, object?, string>((template, parameters, options) => 
                QueueJob(template, parameters, options)));

            // Date Functions
            engine.SetValue("addMinutes", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddMinutes));
            engine.SetValue("addHours", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddHours));
            engine.SetValue("addDays", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddDays));

            // Support for .toList() on arrays/enumerables
            engine.Execute(@"
                Object.prototype.toList = function() {
                    if (Array.isArray(this)) return this;
                    if (this && typeof this.toArray === 'function') return this.toArray();
                    // If it's a .NET List/Enumerable wrapped
                    return new System.Collections.Generic.List(this);
                };
                
                // Polyfill for Array if needed, but Object.prototype hits all. 
                // Better to be specific to Array or standard iterables if possible to avoid polluting everything.
                
                Array.prototype.toList = function() { return this; };
                
                Array.prototype.count = function(predicate) {
                    if (!predicate) return this.length;
                    let c = 0;
                    for (let i = 0; i < this.length; i++) {
                        if (predicate(this[i])) c++;
                    }
                    return c;
                };

                Array.prototype.distinct = function(selector) {
                    let unique = [];
                    let set = new Set();
                    for (let i = 0; i < this.length; i++) {
                        let val = selector ? selector(this[i]) : this[i];
                        let key = typeof val === 'object' && val !== null ? JSON.stringify(val) : val;
                        if (!set.has(key)) {
                            set.add(key);
                            unique.push(val);
                        }
                    }
                    return unique;
                };

                // Override String.prototype.toLowerCase to handle Turkish characters case-insensitively
                const originalToLowerCase = String.prototype.toLowerCase;
                String.prototype.toLowerCase = function() {
                    let str = originalToLowerCase.call(this);
                    let result = '';
                    for (let i = 0; i < str.length; i++) {
                        let c = str[i];
                        if (c === 'I' || c === 'İ' || c === 'ı' || c === 'i') {
                            result += 'i';
                        } else if (c === 'Ö' || c === 'ö') {
                            result += 'ö';
                        } else if (c === 'Ü' || c === 'ü') {
                            result += 'ü';
                        } else if (c === 'Ç' || c === 'ç') {
                            result += 'ç';
                        } else if (c === 'Ş' || c === 'ş') {
                            result += 'ş';
                        } else if (c === 'Ğ' || c === 'ğ') {
                            result += 'ğ';
                        } else {
                            result += c;
                        }
                    }
                    return result.replace(/\u0307/g, '');
                };

                // Case-insensitive contains (alias to includes with case-insensitivity)
                String.prototype.contains = function(str) {
                    if (str === null || str === undefined) return false;
                    if (typeof str !== 'string') str = String(str);
                    return this.toLowerCase().includes(str.toLowerCase());
                };

                // Case-insensitive startsWith
                String.prototype.startsWith = function(str) {
                    if (str === null || str === undefined) return false;
                    if (typeof str !== 'string') str = String(str);
                    return this.toLowerCase().indexOf(str.toLowerCase()) === 0;
                };

                // Array includes check (similar to LINQ Contains/SequenceEqual depending on usage)
                Object.defineProperty(Object.prototype, 'includes', {
                    value: function(arr) {
                        if (Array.isArray(arr)) {
                            // If the item itself is an array, we act like SequenceEqual
                            if (Array.isArray(this.valueOf())) {
                                let self = this.valueOf();
                                if (self.length !== arr.length) return false;
                                for (let i = 0; i < self.length; i++) {
                                    if (self[i] !== arr[i]) return false;
                                }
                                return true;
                            } else {
                                // If it's a single element, act like Contains
                                return arr.includes(this.valueOf());
                            }
                        }
                        if (typeof this.valueOf() === 'string' && typeof arr === 'string') {
                            return this.valueOf().includes(arr);
                        }
                        return false;
                    },
                    enumerable: false,
                    configurable: true,
                    writable: true
                });
            ");
        }

        public void DeleteCollection(string name)
        {
            if (name.StartsWith("sys"))
            {
                throw new InvalidOperationException($"System collection '{name}' cannot be deleted.");
            }

            // --- Backup Query: DeleteCollection ---
            try
            {
                var collection = GetCollection(name);
                var docs = collection.FindAll().ToList();
                if (docs.Count > 0)
                {
                    foreach (var doc in docs)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(doc);
                        var query = $"db.{name}.insert({json})";
                        AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                    }
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for delete collection: {ex.Message}");
            }
            // -----------------------------

            if (_collections.TryRemove(name, out _))
            {
                // Already removed from in-memory dictionary
            }

            // Disk Delete
            var path = Path.Combine(_basePath, "Data", name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            // Cache Invalidation for all documents in this collection
            // Since we use "{Name}:{id}" as key, we can't easily bulk delete from IMemoryCache 
            // without keeping track of all keys or using a different cache structure.
            // However, since the collection object is gone and disk is gone, 
            // the old cache entries will eventually expire. 
            // For a "clean" delete, we'd need a way to scan keys. 
            // Given IMemoryCache doesn't support pattern matching, we'll rely on expiration 
            // OR we accept that orphan cache entries might exist until they expire.
        }

        public DatabaseSettings Settings { get; }

        public DatabaseEngine(string? basePath = null, DatabaseSettings? settings = null)
        {
            Settings = settings ?? new DatabaseSettings();
            _basePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);

            _storage = new AxarDB.Storage.DiskStorage(Path.Combine(_basePath, "Data"));
            _bulkStore = new AxarDB.Bridges.BulkStore(_basePath, Settings.BulkStoreMaxCacheBytes);
            
            // Dynamic Memory Limit
            var gcInfo = GC.GetGCMemoryInfo();
            long totalBytes = gcInfo.TotalAvailableMemoryBytes;
            long limit = (long)(totalBytes * Settings.MemoryLimitPercentage);

            var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
            {
                SizeLimit = limit,
                CompactionPercentage = 0.2
            };
            _sharedCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(cacheOptions);

            Console.WriteLine($"[AxarDB] Database settings loaded:");
            Console.WriteLine($"  - Cache Memory Limit Percentage: {Settings.MemoryLimitPercentage * 100}% (Size limit: {limit / 1024 / 1024} MB)");
            Console.WriteLine($"  - Bulk Store Max Cache Bytes: {Settings.BulkStoreMaxCacheBytes / 1024 / 1024} MB");
            Console.WriteLine($"  - Max Recursion Depth: {Settings.MaxRecursionDepth}");
            Console.WriteLine($"  - Query Timeout Minutes: {Settings.QueryTimeoutMinutes} min");
            Console.WriteLine($"  - Queue Poll Interval: {Settings.QueuePollIntervalSeconds} sec");
            Console.WriteLine($"  - Timezone Offset: {Settings.TimezoneOffset} (UTC{Settings.TimezoneOffset:+#;-#;+0})");

            AxarDB.Logging.Logger.LogDebug($"[AxarDB] Settings: MemoryLimitPercentage={Settings.MemoryLimitPercentage}, BulkStoreMaxCacheBytes={Settings.BulkStoreMaxCacheBytes}, MaxRecursionDepth={Settings.MaxRecursionDepth}, QueryTimeoutMinutes={Settings.QueryTimeoutMinutes}, QueuePollIntervalSeconds={Settings.QueuePollIntervalSeconds}, TimezoneOffset={Settings.TimezoneOffset}");

            // Create default system collection
            GetCollection("sysusers");
            GetCollection("sysqueue"); // Initialize queue collection
            GetCollection("sysconfig"); // Initialize config collection
            GetCollection("syslogs"); // Initialize logs collection
            // Add default user
            var sysusers = GetCollection("sysusers");
            // Check via storage if empty
            if (!sysusers.FindAll().Any())
            {
                sysusers.Insert(new Dictionary<string, object>
                {
                    { "username", "unlocker" },
                    { "password", "unlocker" }
                });
            }

            // Seed default configuration settings if empty
            var sysconfig = GetCollection("sysconfig");
            var existingConfigs = sysconfig.FindAll().ToList();
            var defaultConfigs = new Dictionary<string, object>
            {
                { "memoryLimitPercentage", Settings.MemoryLimitPercentage },
                { "bulkStoreMaxCacheBytes", Settings.BulkStoreMaxCacheBytes },
                { "maxRecursionDepth", Settings.MaxRecursionDepth },
                { "queryTimeoutMinutes", Settings.QueryTimeoutMinutes },
                { "queuePollIntervalSeconds", Settings.QueuePollIntervalSeconds },
                { "timezoneOffset", Settings.TimezoneOffset }
            };

            if (!existingConfigs.Any())
            {
                sysconfig.Insert(defaultConfigs, bypassSystemRules: true);
            }
            else
            {
                var currentConfig = existingConfigs.First();
                var updatedConfig = new Dictionary<string, object>(currentConfig);
                bool changed = false;

                foreach (var kvp in defaultConfigs)
                {
                    if (!currentConfig.ContainsKey(kvp.Key))
                    {
                        updatedConfig[kvp.Key] = kvp.Value;
                        changed = true;
                    }
                }

                if (changed)
                {
                    sysconfig.UpdateExisting(updatedConfig, currentConfig, bypassSystemRules: true);
                }
            }
        }

        public Collection GetCollection(string name)
        {
            return _collections.GetOrAdd(name, n => new Collection(n, _storage, _sharedCache));
        }

        public List<string> GetCollections()
        {
            var list = new HashSet<string>();
            foreach (var key in _collections.Keys)
            {
                if (key.StartsWith("sys"))
                {
                    list.Add(key);
                }
            }
            list.Add("syslogs");
            var dataPath = Path.Combine(_basePath, "Data");
            if (Directory.Exists(dataPath))
            {
                var dirs = Directory.GetDirectories(dataPath).Select(Path.GetFileName).Where(n => n != null);
                foreach (var dir in dirs)
                {
                    list.Add(dir!);
                }
            }
            return list.OrderBy(x => x).ToList();
        }

        public object? ExecuteScript(string script, System.Collections.Generic.IDictionary<string, object>? parameters = null, ScriptContext? context = null, CancellationToken cancellationToken = default)
        {
            var ctx = context ?? ScriptContext.Default; 
            AxarDB.Logging.Logger.LogDebug($"[Engine] Executing script:\n{script}");
            // ---------------------------------------------------------
            // VAULTS FEATURE INITIALIZATION
            // ---------------------------------------------------------
            GetCollection("sysvaults");

            //FOR AI TESTING PURPOSES
            // Validate and prepare script
            // 1. Vault Replacement ($key -> value from sysvaults)
            // We do this BEFORE parameters to allow vaults to define structure if needed, 
            // though usually independent. 
            // Fetch all vaults to replace placeholders.
            var vaultCol = GetCollection("sysvaults");
            // Optimization: Only fetch if script contains '$'
            if (script.Contains("$"))
            {
                // We stream all vaults. For large number of vaults this might be slow, 
                // but usually vaults are few (config).
                foreach (var vDoc in vaultCol.FindAll())
                {
                    if (vDoc.TryGetValue("key", out var k) && vDoc.TryGetValue("value", out var v))
                    {
                        string? keyStr = k.ToString();
                        // Replace $key with serialized value.
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            var valStr = System.Text.Json.JsonSerializer.Serialize(v);
                            script = script.Replace("$" + keyStr, valStr);
                        }
                    }
                }
            }

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // 1. Validation: simple heuristic blacklist
                    if (param.Value is string s && !IsValidInput(s))
                    {
                        throw new InvalidOperationException($"Input parameter '{param.Key}' contains potentially malicious content.");
                    }

                    //FOR AI TESTING PURPOSES
                    // 2. Placeholder Replacement
                    // We look for @Key and replace it with JSON serialized Value
                    // This ensures strings are quoted and special chars escaped.
                    // Example: @name -> "ketty" or "ke\"tty"
                    
                    var serializedValue = System.Text.Json.JsonSerializer.Serialize(param.Value);
                    
                    // Using simple String.Replace for now as per plan
                    script = script.Replace("@" + param.Key, serializedValue);
                }
            }

            // Build a fresh Jint engine per script execution. The engine captures
            // the cancellation token at construction time, so a cached/shared
            // engine would permanently bind to the first request's abort token
            // and cancel every subsequent script once that request completes.
            var engine = CreateScriptEngine(cancellationToken, ctx);

            // Execute user script. A fresh engine per call keeps each request's
            // cancellation token and global scope isolated.
            var result = engine.Evaluate(script);

            // Convert result back to native object if possible
            var obj = result.ToObject();
            
            // Auto-materialize queries to list if not done by user
            if (obj is JoinCollectionBridge joinBridge)
            {
                return joinBridge.toList();
            }
            if (obj is ResultSet resultSet)
            {
                return resultSet.ToList();
            }
            if (obj is CollectionBridge colBridge)
            {
                return colBridge.findall().ToList();
            }
            if (obj is AxarDB.Bridges.MemoryResultSet mrs)
            {
                return mrs.ToList();
            }
            if (obj is AxarDB.Bridges.BulkResultSet brs)
            {
                return brs.ToList();
            }
            if (obj is AxarDB.Bridges.MemoryCollectionBridge mcb)
            {
                return mcb.findall().ToList();
            }
            if (obj is AxarDB.Bridges.BulkCollectionBridge bcb)
            {
                return bcb.findall().ToList();
            }
            if (obj is AxarDB.Bridges.LogCollectionBridge lcb)
            {
                return lcb.findall().ToList();
            }

            return obj;
        }

        /// <summary>
        /// Creates a fully configured Jint engine bound to the supplied
        /// cancellation token. Called once per <see cref="ExecuteScript"/> so the
        /// engine never inherits a stale request-aborted token from a previous call.
        /// </summary>
        private Engine CreateScriptEngine(CancellationToken cancellationToken, ScriptContext ctx)
        {
            var engine = new Engine(options => {
                 options.AllowClr();
                 options.CancellationToken(cancellationToken);
                 options.LimitRecursion(Settings.MaxRecursionDepth);
                 options.TimeoutInterval(TimeSpan.FromMinutes(Settings.QueryTimeoutMinutes));
             });

            // Expose console.log for CLI scripts and debugging
            engine.SetValue("console", new { log = new Action<object>(o => AxarDB.Logging.Logger.LogDebug(FormatLog(o))) });

            // Expose 'db'
            var dbBridge = new AxarDBBridge(this, engine, cancellationToken);
            engine.SetValue("db", dbBridge);

            // Expose 'memory' — top-level in-memory store with TTL support
            var memoryBridge = new AxarDB.Bridges.MemoryBridge(_memoryStore, engine, cancellationToken);
            engine.SetValue("memory", memoryBridge);

            // Expose 'bulk' — top-level JSONL store
            var bulkBridge = new AxarDB.Bridges.BulkBridge(_bulkStore, engine);
            engine.SetValue("bulk", bulkBridge);

            // Expose 'log' — read-only log store
            var logBridge = new AxarDB.Bridges.LogBridge(_basePath, engine, cancellationToken);
            engine.SetValue("log", logBridge);

            // Expose 'UnlockDB' constructor: new UnlockDB("name")
            engine.SetValue("AxarDB", new Func<string, CollectionBridge>(name => {
                if (name.StartsWith("sys", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("sysusers", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("sysqueue", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("sysvaults", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("sysconfig", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("syslogs", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"System collection name '{name}' is reserved.");
                }
                return new CollectionBridge(this, GetCollection(name), engine, cancellationToken);
            }));

            // Expose 'showCollections'
            engine.SetValue("showCollections", new Func<List<string>>(() => GetCollections()));

            engine.SetValue("getIndexes", new Func<string, object>((name) => {
                var col = GetCollection(name);
                return col.Indices.Select(i => new { PropertyName = i.PropertyName, Type = i.Type }).ToList();
            }));

            engine.SetValue("addVault", new Func<string, object, bool>((key, value) => AddVault(key, value)));

            // --- Utility Functions ---
            RegisterUtils(engine, ctx);

            return engine;
        }

        private bool IsValidInput(string input)
        {
            var blackList = new[] 
            { 
                "eval(", "Function(", "setTimeout(", "setInterval(", "<script", "javascript:" 
            };

            foreach (var item in blackList)
            {
                if (input.Contains(item, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        public bool Authenticate(string username, string password)
        {
            AxarDB.Logging.Logger.LogDebug($"[Auth] Authenticating user '{username}'");
            var sysusers = GetCollection("sysusers");
            
            // Try matching plain password first
            var result = sysusers.FindAll(d => 
                d.ContainsKey("username") && d["username"].ToString() == username &&
                d.ContainsKey("password") && d["password"].ToString() == password
            ).Any();

            // If not found, try matching with SHA256 hash (in case the password in DB is hashed)
            if (!result)
            {
                string hashedPassword = AxarDB.Helpers.ScriptUtils.SHA256(password);
                result = sysusers.FindAll(d => 
                    d.ContainsKey("username") && d["username"].ToString() == username &&
                    d.ContainsKey("password") && d["password"].ToString() == hashedPassword
                ).Any();
            }

            AxarDB.Logging.Logger.LogDebug($"[Auth] Result for '{username}': {result}");
            return result;
        }
    
        public bool AddVault(string key, object value)
        {
            var col = GetCollection("sysvaults");
            // Check if exists
            var existing = col.FindAll().FirstOrDefault(d => d.ContainsKey("key") && d["key"]?.ToString() == key);
            
            if (existing != null)
            {
                // Update
                existing["value"] = value;
                col.Insert(existing); // Persist update
            }
            else
            {
                // Insert new
                col.Insert(new Dictionary<string, object> { 
                    { "key", key }, 
                    { "value", value },
                    { "created", ServerTime.Now }
                });
            }
            return true;
        }

        private string QueueJob(string template, object? parameters, object? options)
        {
            var sysqueue = GetCollection("sysqueue");
            var id = AxarDB.Helpers.GuidV7.NewGuid().ToString();
            
            var job = new Dictionary<string, object>
            {
                { "_id", id },
                { "queryTemplate", template },
                { "parameters", parameters! }, // Stored as provided (Dict or Array)
                { "options", options! },
                { "createdAt", ServerTime.Now },
                { "executionTime", null! }, // null means pending
                { "priority", 0 }, // Default priority
                { "duration", 0 },
                { "successResult", null! },
                { "errorMessage", null! },
                { "completedAt", null! }
            };

            // Handle options if provided
            if (options != null)
            {
                // Try to extract priority
                try 
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(options);
                    var dict = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(json) as System.Collections.Generic.IDictionary<string, object>;
                    if (dict != null && dict.ContainsKey("priority"))
                    {
                         job["priority"] = Convert.ToInt32(dict["priority"]);
                    }
                }
                catch {}
            }

            sysqueue.Insert(job);
            return id;
        }

        // ---------------------------------------------------------
        // VIEWS FEATURE
        // ---------------------------------------------------------

        private string GetViewsPath() 
        {
            var path = Path.Combine(_basePath, "Views");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string GetLogsPath() 
        {
            var path = Path.Combine(_basePath, "view_logs");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public object? ExecuteView(string viewName, System.Collections.Generic.IDictionary<string, object>? parameters, string clientIp, string user, CancellationToken cancellationToken = default)
        {
            var context = new ScriptContext 
            { 
               IpAddress = clientIp, 
               User = user, 
               IsView = true, 
               ViewName = viewName 
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var consoleLogs = new List<string>();
            object? result = null;
            string? error = null;

            try
            {
                var viewPath = Path.Combine(GetViewsPath(), viewName + ".js");
                if (!File.Exists(viewPath)) throw new FileNotFoundException($"View '{viewName}' not found.");

                var script = File.ReadAllText(viewPath, Encoding.UTF8);
                
                //FOR AI TESTING PURPOSES
                // Access Control Check (for HTTP calls mostly, but good to enforce)
                // If called internally via db.view, we assume privileged.
                // If called via HTTP, Program.cs handles Auth. 
                // But we need to know if it's public/private for Program.cs logic.
                // We'll expose a method GetViewAccess(viewName) for that.

                // Execute script using standard method but with Console injection
                
                // We reuse ExecuteScript logic but need to inject 'console'
                // Refactoring ExecuteScript to take an action for engine config would be cleaner, 
                // but let's duplicate/inline slightly for minimal disruption or use a protected override.
                // Actually, let's just instantiate engine here or modify ExecuteScript to support Action<Engine>.
                
                // We'll implement a custom execution here to support console capture + logging specific to views.
                
                // 1. Prepare Script (Vaults + Params) - Reuse logic?
                // We can't easily reuse ExecuteScript private logic without refactor. 
                // Let's refactor `PrepareScript` out of `ExecuteScript`.
                
                // For now, I will inline the prep logic to ensure it works correctly for Views specific requirements.
                
                // 1. Vaults
                if (script.Contains("$"))
                {
                    var vaultCol = GetCollection("sysvaults");
                    foreach (var vDoc in vaultCol.FindAll())
                    {
                        if (vDoc.TryGetValue("key", out var k) && vDoc.TryGetValue("value", out var v))
                        {
                             if (k != null) script = script.Replace("$" + k.ToString(), System.Text.Json.JsonSerializer.Serialize(v));
                        }
                    }
                }

                // 2. Params
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        if (param.Value is string s && !IsValidInput(s)) throw new InvalidOperationException($"Malicious input in '{param.Key}'");
                        script = script.Replace("@" + param.Key, System.Text.Json.JsonSerializer.Serialize(param.Value));
                    }
                }

                
                var engine = new Engine(options => {
                     options.AllowClr();
                     options.CancellationToken(cancellationToken);
                     options.LimitRecursion(Settings.MaxRecursionDepth);
                     options.TimeoutInterval(TimeSpan.FromMinutes(Settings.QueryTimeoutMinutes));
                });
                
                // Bridges
                var dbBridge = new AxarDBBridge(this, engine, cancellationToken);
                engine.SetValue("db", dbBridge);
                engine.SetValue("AxarDB", new Func<string, CollectionBridge>(name => new CollectionBridge(this, GetCollection(name), engine, cancellationToken)));
                engine.SetValue("showCollections", new Func<List<string>>(() => GetCollections())); // Simplified for view

                // Expose 'memory' — top-level in-memory store with TTL support
                var memoryBridge = new AxarDB.Bridges.MemoryBridge(_memoryStore, engine, cancellationToken);
                engine.SetValue("memory", memoryBridge);

                // Expose 'bulk' — top-level JSONL store
                var bulkBridge = new AxarDB.Bridges.BulkBridge(_bulkStore, engine);
                engine.SetValue("bulk", bulkBridge);

                // Expose 'log' — read-only log store
                var logBridge = new AxarDB.Bridges.LogBridge(_basePath, engine, cancellationToken);
                engine.SetValue("log", logBridge);
                RegisterUtils(engine, context);
                // Ideally ExecuteScript should be refactored. 
                // Let's just add the Console capture here.
                
                engine.SetValue("console", new { log = new Action<object>(o => consoleLogs.Add(FormatLog(o))) });

                // Execute
                var evalResult = engine.Evaluate(script);
                result = evalResult.ToObject();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                throw;
            }
            finally
            {
                sw.Stop();
                AxarDB.Metrics.MetricsCollector.Instance.RecordScript("view", viewName, sw.ElapsedMilliseconds, error == null, error);
                
                // Logging
                var logEntry = new 
                {
                    clientIp,
                    user,
                    timestamp = ServerTime.Now,
                    durationMs = sw.ElapsedMilliseconds,
                    error,
                    console = consoleLogs,
                    result = error != null ? null : result // Don't log full result if massive? Maybe option.
                };
                
                var logJson = System.Text.Json.JsonSerializer.Serialize(logEntry, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(GetLogsPath(), $"{viewName}_{ServerTime.Now.Ticks}.json"), logJson, Encoding.UTF8);
            }
            
            return result;
        }

        public string GetViewAccess(string viewName)
        {
            var viewPath = Path.Combine(GetViewsPath(), viewName + ".js");
            if (!File.Exists(viewPath)) return "private"; 
            
            // Read first few lines
            foreach(var line in File.ReadLines(viewPath, Encoding.UTF8).Take(5))
            {
                if (line.Contains("@access public")) return "public";
            }
            return "private";
        }

        public void SaveView(string name, string content)
        {
            // --- Backup Query: SaveView ---
            try
            {
                var oldContent = GetViewContent(name);
                var query = oldContent != null 
                    ? $"db.saveView('{name}', {System.Text.Json.JsonSerializer.Serialize(oldContent)})" 
                    : $"db.deleteView('{name}')";
                AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for SaveView: {ex.Message}");
            }
            // -----------------------------

            File.WriteAllText(Path.Combine(GetViewsPath(), name + ".js"), content, Encoding.UTF8);
        }

        public void DeleteView(string name)
        {
            // --- Backup Query: DeleteView ---
            try
            {
                var oldContent = GetViewContent(name);
                if (oldContent != null)
                {
                    var query = $"db.saveView('{name}', {System.Text.Json.JsonSerializer.Serialize(oldContent)})";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for DeleteView: {ex.Message}");
            }
            // -----------------------------

            var path = Path.Combine(GetViewsPath(), name + ".js");
            if (File.Exists(path)) File.Delete(path);
        }


        public List<string> ListViews()
        {
            var path = GetViewsPath();
            return Directory.GetFiles(path, "*.js").Select(Path.GetFileNameWithoutExtension).Cast<string>().ToList();
        }

        public string? GetViewContent(string name)
        {
            var path = Path.Combine(GetViewsPath(), name + ".js");
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        // ---------------------------------------------------------
        // TRIGGERS FEATURE
        // ---------------------------------------------------------

        private FileSystemWatcher? _triggerWatcher;

        private string GetTriggersPath() 
        {
            var path = Path.Combine(_basePath, "Triggers");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string GetTriggerLogsPath() 
        {
            var path = Path.Combine(_basePath, "trigger_logs");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public void InitializeTriggers()
        {
            if (_triggerWatcher != null) return;

            
            var dataPath = Path.Combine(_basePath, "Data");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

            _triggerWatcher = new FileSystemWatcher(dataPath);
            _triggerWatcher.IncludeSubdirectories = true;
            _triggerWatcher.Filter = "*.json"; // Only listen to data changes
            // Watch for changes, creation, deletion
            _triggerWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;

            _triggerWatcher.Changed += (s, e) => HandleFileEvent(e.FullPath, "changed");
            _triggerWatcher.Created += (s, e) => HandleFileEvent(e.FullPath, "created");
            _triggerWatcher.Deleted += (s, e) => HandleFileEvent(e.FullPath, "deleted");
            
            _triggerWatcher.EnableRaisingEvents = true;
        }

        private void HandleFileEvent(string fullPath, string evtType)
        {
            Task.Run(() => 
            {
                try 
                {
                    // 1. Identify Collection and Document
                    // Path: Data/CollectionName/doc.json OR Data/CollectionName/_id_wrapper if shards (but current impl is simple)
                    // Current DiskStorage: Data/CollectionName/{guid}.json
                    
                    // Index files (idx_*.json) are metadata, not documents — ignore them so the
                    // watcher doesn't treat index writes as document changes.
                    if (Path.GetFileName(fullPath).StartsWith("idx_", StringComparison.OrdinalIgnoreCase))
                        return;

                    var relative = Path.GetRelativePath(Path.Combine(_basePath, "Data"), fullPath);
                    var parts = relative.Split(Path.DirectorySeparatorChar);
                    
                    if (parts.Length < 2) return; // Not inside a collection
                    
                    var collectionName = parts[0];
                    var docIdRaw = Path.GetFileNameWithoutExtension(parts[1]);

                    // Notify Collection for Cache Invalidation
                    try
                    {
                        var collection = GetCollection(collectionName);
                        collection.OnExternalChange(docIdRaw, evtType);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying collection change: {ex.Message}");
                    }
                    
                    // 2. Find matching triggers
                    var triggers = Directory.GetFiles(GetTriggersPath(), "*.js");
                    
                    foreach(var triggerPath in triggers)
                    {
                        var content = File.ReadAllText(triggerPath, Encoding.UTF8);
                        // Parse header: // @target collectionName
                        if (content.Contains($"@target {collectionName}") || content.Contains("@target *"))
                        {
                            ExecuteTrigger(Path.GetFileNameWithoutExtension(triggerPath), content, evtType, collectionName, docIdRaw);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTriggerError("System", ex.Message);
                }
            });
        }

        private void ExecuteTrigger(string triggerName, string script, string type, string col, string docId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var consoleLogs = new List<string>();
            string? error = null;

            try
            {
                var engine = new Engine(options => options.AllowClr());
                
                // Minimal Bridge for Triggers - full db access? Yes.
                var dbBridge = new AxarDBBridge(this, engine);
                engine.SetValue("db", dbBridge);
                RegisterUtils(engine, ScriptContext.Default); // Triggers run as system currently
                // Re-add console
                engine.SetValue("console", new { log = new Action<object>(o => consoleLogs.Add(FormatLog(o))) });

                // Event Object
                engine.SetValue("event", new 
                {
                    type,
                    collection = col,
                    documentId = docId,
                    timestamp = ServerTime.Now
                });

                engine.Evaluate(script);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                consoleLogs.Add("Error: " + ex.Message);
            }
            finally
            {
                sw.Stop();
                LogTriggerExecution(triggerName, sw.ElapsedMilliseconds, consoleLogs, error);
                AxarDB.Metrics.MetricsCollector.Instance.RecordScript("trigger", triggerName, sw.ElapsedMilliseconds, error == null, error);
            }
        }

        private void LogTriggerExecution(string triggerName, long duration, List<string> logs, string? error)
        {
            try 
            {
                var logEntry = new 
                {
                    trigger = triggerName,
                    timestamp = ServerTime.Now,
                    durationMs = duration,
                    error,
                    console = logs
                };
                
                var line = System.Text.Json.JsonSerializer.Serialize(logEntry);
                var filename = $"{ServerTime.Now:yyyy-MM-dd}.log";
                var path = Path.Combine(GetTriggerLogsPath(), filename);
                
                // Simple file append with lock to ensure thread safety
                lock(_logLock) 
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            } 
            catch {}
        }

        private void LogTriggerError(string context, string msg)
        {
            LogTriggerExecution(context, 0, new List<string> { msg }, "System Error");
        }

        private static readonly object _logLock = new object();


    
        public string? GetTriggerContent(string name)
        {
            var path = Path.Combine(GetTriggersPath(), name + ".js");
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        public void SaveTrigger(string name, string targetCollection, string content)
        {
            // --- Backup Query: SaveTrigger ---
            try
            {
                var oldContent = GetTriggerContent(name);
                if (oldContent != null)
                {
                    var query = $"db.saveTrigger('{name}', '{targetCollection}', {System.Text.Json.JsonSerializer.Serialize(oldContent)})";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
                else
                {
                    var query = $"db.deleteTrigger('{name}')";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for SaveTrigger: {ex.Message}");
            }
            // -----------------------------

            if (!content.Contains("@target"))
            {
                content = $"// @target {targetCollection}\n" + content;
            }
            File.WriteAllText(Path.Combine(GetTriggersPath(), name + ".js"), content, Encoding.UTF8);
        }
        
        public void SaveTrigger(string name, string content) => SaveTrigger(name, "*", content);

        public void DeleteTrigger(string name)
        {
            // --- Backup Query: DeleteTrigger ---
            try
            {
                var oldContent = GetTriggerContent(name);
                if (oldContent != null)
                {
                    var query = $"db.saveTrigger('{name}', '*', {System.Text.Json.JsonSerializer.Serialize(oldContent)})";
                    AxarDB.Core.BackupQuery.LogRecoveryQuery(_basePath, query);
                }
            }
            catch (Exception ex)
            {
                AxarDB.Logging.Logger.LogError($"[BackupQuery Error] Failed to create backup query for DeleteTrigger: {ex.Message}");
            }
            // -----------------------------

            var path = Path.Combine(GetTriggersPath(), name + ".js");
            if (File.Exists(path)) File.Delete(path);
        }

        public List<string> ListTriggers()
        {
            return Directory.GetFiles(GetTriggersPath(), "*.js").Select(Path.GetFileNameWithoutExtension).Cast<string>().ToList();
        }

        public object PgSqlRead(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? error = null;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(query, connection);
                
                if (parameters != null)
                {
                    var paramJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var paramDict = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(paramJson) as System.Collections.Generic.IDictionary<string, object>;
                    
                    if (paramDict != null)
                    {
                        foreach (var kvp in paramDict)
                        {
                            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                        }
                    }
                }
                
                using var reader = command.ExecuteReader();
                var results = new List<Dictionary<string, object>>();
                
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i);
                        if (val == DBNull.Value) val = null;
                        row[reader.GetName(i)] = val!;
                    }
                    results.Add(row);
                }
                
                sw.Stop();
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                error = ex.Message;
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, error);
                throw new Exception($"PostgreSQL Read Failed: {ex.Message}");
            }
        }

        public int PgSqlExec(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? error = null;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(query, connection);
                
                if (parameters != null)
                {
                    var paramJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var paramDict = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(paramJson) as System.Collections.Generic.IDictionary<string, object>;
                    
                    if (paramDict != null)
                    {
                        foreach (var kvp in paramDict)
                        {
                            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                        }
                    }
                }
                
                int affected = command.ExecuteNonQuery();
                
                sw.Stop();
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return affected;
            }
            catch (Exception ex)
            {
                sw.Stop();
                error = ex.Message;
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, error);
                throw new Exception($"PostgreSQL Exec Failed: {ex.Message}");
            }
        }

        private void LogPgSqlRequest(ScriptContext context, string connectionString, string query, object? parameters, long durationMs, bool success, string? error = null)
        {
             try
             {
                 // Use AxarDB.Logging.Logger to avoid ambiguity
                 AxarDB.Logging.Logger.LogRequest(context.IpAddress, context.User, $"[PostgreSQL] {query}", durationMs, success, error ?? "");
                 
                 if (!success && !string.IsNullOrEmpty(error))
                 {
                     AxarDB.Logging.Logger.LogError($"[PostgreSQL Error] {error} | Query: {query}");
                 }
             }
             catch {}
        }
    }
}
