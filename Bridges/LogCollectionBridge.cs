using Jint;
using Jint.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AxarDB.Wrappers;
using AxarDB.Core;
using System.Text;
using AxarDB.Definitions;

namespace AxarDB.Bridges
{
    public class LogCollectionBridge
    {
        private readonly string _basePath;
        private readonly string _type;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public LogCollectionBridge(string basePath, string type, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _basePath = basePath;
            _type = type;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        private IEnumerable<Dictionary<string, object>> LoadLogs()
        {
            if (_type.Equals("syslogs", StringComparison.OrdinalIgnoreCase) || _type.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var allLogs = new List<Dictionary<string, object>>();
                allLogs.AddRange(LoadLogsForType("request"));
                allLogs.AddRange(LoadLogsForType("error"));
                allLogs.AddRange(LoadLogsForType("debug"));

                return allLogs.OrderByDescending(l => l.TryGetValue("timestamp", out var ts) ? ts?.ToString() : "");
            }

            return LoadLogsForType(_type);
        }

        private IEnumerable<Dictionary<string, object>> LoadLogsForType(string type)
        {
            string folderName = type switch
            {
                "request" => "request_logs",
                "error" => "error_logs",
                "debug" => "debug_logs",
                _ => type + "_logs"
            };

            var dirPath = Path.Combine(_basePath, folderName);
            if (!Directory.Exists(dirPath))
            {
                return Enumerable.Empty<Dictionary<string, object>>();
            }

            var list = new List<Dictionary<string, object>>();
            try
            {
                var files = Directory.GetFiles(dirPath, "*.txt").OrderByDescending(f => f);
                foreach (var file in files)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var lines = File.ReadAllLines(file, Encoding.UTF8);
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        Dictionary<string, object>? parsed = null;
                        if (type == "request")
                        {
                            parsed = ParseRequestLogLine(line);
                        }
                        else if (type == "error")
                        {
                            parsed = ParseErrorLogLine(line);
                        }
                        else if (type == "debug")
                        {
                            parsed = ParseDebugLogLine(line);
                        }
                        else
                        {
                            parsed = new Dictionary<string, object>
                            {
                                { "_id", AxarDB.Helpers.GuidV7.NewGuid().ToString() },
                                { "timestamp", ServerTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                                { "type", type },
                                { "ip", "" },
                                { "user", "" },
                                { "query", line },
                                { "request", line },
                                { "durationMs", 0L },
                                { "status", "Info" }
                            };
                        }

                        if (parsed != null)
                        {
                            list.Add(parsed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogBridge] Error reading logs: {ex.Message}");
            }

            return list;
        }

        private static string GenerateLogId(string timestampStr)
        {
            if (DateTimeOffset.TryParse(timestampStr, out var dto))
            {
                return AxarDB.Helpers.GuidV7.NewGuid(dto).ToString();
            }
            return AxarDB.Helpers.GuidV7.NewGuid().ToString();
        }

        private Dictionary<string, object>? ParseRequestLogLine(string line)
        {
            try
            {
                int tsStart = line.IndexOf('[');
                if (tsStart == -1) return null;
                int tsEnd = line.IndexOf(']', tsStart);
                if (tsEnd == -1) return null;
                string timestamp = line.Substring(tsStart + 1, tsEnd - tsStart - 1);

                int ipStart = line.IndexOf('[', tsEnd + 1);
                if (ipStart == -1) return null;
                int ipEnd = line.IndexOf(']', ipStart);
                if (ipEnd == -1) return null;
                string ip = line.Substring(ipStart + 1, ipEnd - ipStart - 1);

                int userStart = line.IndexOf('[', ipEnd + 1);
                if (userStart == -1) return null;
                int userEnd = line.IndexOf(']', userStart);
                if (userEnd == -1) return null;
                string user = line.Substring(userStart + 1, userEnd - userStart - 1);

                int statusEnd = line.LastIndexOf(']');
                if (statusEnd == -1) return null;
                int statusStart = line.LastIndexOf('[', statusEnd);
                if (statusStart == -1) return null;
                string status = line.Substring(statusStart + 1, statusEnd - statusStart - 1);

                int durationEnd = line.LastIndexOf(']', statusStart - 1);
                if (durationEnd == -1) return null;
                int durationStart = line.LastIndexOf('[', durationEnd);
                if (durationStart == -1) return null;
                string durationStr = line.Substring(durationStart + 1, durationEnd - durationStart - 1);
                if (durationStr.EndsWith("ms")) durationStr = durationStr.Substring(0, durationStr.Length - 2);
                long.TryParse(durationStr, out long durationMs);

                int jsonStart = userEnd + 5;
                int jsonEnd = durationStart - 4;
                string rawJson = "";
                if (jsonEnd >= jsonStart && jsonEnd <= line.Length)
                {
                    rawJson = line.Substring(jsonStart, jsonEnd - jsonStart);
                }

                return new Dictionary<string, object>
                {
                    { "_id", GenerateLogId(timestamp) },
                    { "timestamp", timestamp },
                    { "type", "request" },
                    { "ip", ip },
                    { "user", user },
                    { "query", rawJson },
                    { "request", rawJson },
                    { "durationMs", durationMs },
                    { "status", status }
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, object>? ParseErrorLogLine(string line)
        {
            try
            {
                int tsStart = line.IndexOf('[');
                if (tsStart == -1) return null;
                int tsEnd = line.IndexOf(']', tsStart);
                if (tsEnd == -1) return null;
                string timestamp = line.Substring(tsStart + 1, tsEnd - tsStart - 1);
                string message = line.Substring(tsEnd + 1).Trim();

                return new Dictionary<string, object>
                {
                    { "_id", GenerateLogId(timestamp) },
                    { "timestamp", timestamp },
                    { "type", "error" },
                    { "ip", "" },
                    { "user", "" },
                    { "query", message },
                    { "request", message },
                    { "durationMs", 0L },
                    { "status", "Failed: " + message }
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, object>? ParseDebugLogLine(string line)
        {
            try
            {
                int tsStart = line.IndexOf('[');
                if (tsStart == -1) return null;
                int tsEnd = line.IndexOf(']', tsStart);
                if (tsEnd == -1) return null;
                string timestamp = line.Substring(tsStart + 1, tsEnd - tsStart - 1);
                
                string remainder = line.Substring(tsEnd + 1).Trim();
                if (remainder.StartsWith("[DEBUG]"))
                {
                    remainder = remainder.Substring(7).Trim();
                }

                return new Dictionary<string, object>
                {
                    { "_id", GenerateLogId(timestamp) },
                    { "timestamp", timestamp },
                    { "type", "debug" },
                    { "ip", "" },
                    { "user", "" },
                    { "query", remainder },
                    { "request", remainder },
                    { "durationMs", 0L },
                    { "status", "Debug" }
                };
            }
            catch
            {
                return null;
            }
        }

        public ResultSet findall(JsValue? predicate = null)
        {
            var logs = LoadLogs();
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                return new ResultSet(logs, null);
            }

            Func<Dictionary<string, object>, bool> csPredicate = (d) => 
            {
                 if (_engine == null) return true;
                 lock (_engine)
                 {
                     try
                     {
                        var result = _engine.Invoke(predicate, new object[] { new Wrappers.DocumentWrapper(d) });
                        return result.AsBoolean();
                     }
                     catch { return false; }
                 }
            };

            var filtered = logs.Where(csPredicate);
            return new ResultSet(filtered, null);
        }

        public Wrappers.DocumentWrapper? find(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) => 
            {
                if (_engine == null) return predicate(new Wrappers.DocumentWrapper(d));
                lock (_engine) 
                {
                    try { return predicate(new Wrappers.DocumentWrapper(d)); } catch { return false; }
                }
            };

            var doc = LoadLogs().FirstOrDefault(safePredicate);
            return doc != null ? new Wrappers.DocumentWrapper(doc) : null;
        }

        public AxarList select(Func<object, object> selector)
        {
            Func<Dictionary<string, object>, object> safeSelector = (d) => 
            {
                if (_engine == null) return selector(new Wrappers.DocumentWrapper(d));
                lock (_engine) { return selector(new Wrappers.DocumentWrapper(d)); }
            };

            var list = LoadLogs().Select(safeSelector);
            return new AxarList(list);
        }

        public Wrappers.DocumentWrapper? first()
        {
            var firstDoc = LoadLogs().FirstOrDefault();
            return firstDoc != null ? new Wrappers.DocumentWrapper(firstDoc) : null;
        }

        public ResultSet take(int count) => findall().take(count);

        public ResultSet skip(int count) => findall().skip(count);

        public List<Dictionary<string, object>> toList() => findall().toList();

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return LoadLogs().Count();
            
            Func<Dictionary<string, object>, bool> safePredicate = (d) => 
            {
                if (_engine == null) return predicate(new Wrappers.DocumentWrapper(d));
                lock (_engine) 
                {
                    try { return predicate(new Wrappers.DocumentWrapper(d)); } catch { return false; }
                }
            };
            return LoadLogs().Count(safePredicate);
        }

        public void insert(object doc)
        {
            throw new InvalidOperationException("Modification is not allowed on syslogs system collection.");
        }

        public void update(object updateFields)
        {
            throw new InvalidOperationException("Modification is not allowed on syslogs system collection.");
        }

        public void delete()
        {
            throw new InvalidOperationException("Modification is not allowed on syslogs system collection.");
        }
    }
}
