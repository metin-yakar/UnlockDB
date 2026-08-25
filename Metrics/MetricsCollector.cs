using System.Collections.Concurrent;
using AxarDB.Definitions;

namespace AxarDB.Metrics
{
    /// <summary>
    /// Thread-safe in-memory metrics collector.
    /// Tracks HTTP requests, query performance, view/trigger/queue costs, and system resources.
    /// Memory footprint is capped: oldest entries are evicted when limits are exceeded.
    /// </summary>
    public class MetricsCollector
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static readonly MetricsCollector Instance = new();
        private readonly System.Threading.Timer _systemTimer;
        private MetricsCollector() 
        {
            _systemTimer = new System.Threading.Timer(_ => RecordSystemSample(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        // ─── Request Log ─────────────────────────────────────────────────────
        private readonly ConcurrentQueue<RequestMetric> _requests = new();
        private int _requestCount = 0;
        private const int MaxRequests = 10000;

        // ─── Script Cost Log ─────────────────────────────────────────────────
        private readonly ConcurrentQueue<ScriptMetric> _scripts = new();
        private const int MaxScripts = 5000;

        // ─── System Samples ──────────────────────────────────────────────────
        private readonly ConcurrentQueue<SystemSample> _system = new();
        private const int MaxSamples = 1440; // 24h at 1-min intervals

        // ─── View / Trigger / Queue Aggregates ───────────────────────────────
        private readonly ConcurrentDictionary<string, AggregateMetric> _viewMetrics = new();
        private readonly ConcurrentDictionary<string, AggregateMetric> _triggerMetrics = new();
        private readonly ConcurrentDictionary<string, AggregateMetric> _queueMetrics = new();

        // ─── Models ───────────────────────────────────────────────────────────

        public record RequestMetric(DateTime Timestamp, string Ip, string Method, string Path, int StatusCode, long DurationMs, long RequestBytes, long ResponseBytes);
        public record ScriptMetric(DateTime Timestamp, string Type, string Name, long DurationMs, bool Success, string? Error);
        public record SystemSample(DateTime Timestamp, long RamMB, double CpuPercent);

        public class AggregateMetric
        {
            public long CallCount;
            public long TotalMs;
            public long ErrorCount;
            public long MinMs = long.MaxValue;
            public long MaxMs;
            public DateTime LastCalled;
        }

        // ─── Record Methods ───────────────────────────────────────────────────

        public void RecordRequest(string ip, string method, string path, int statusCode, long durationMs, long reqBytes = 0, long resBytes = 0)
        {
            var m = new RequestMetric(ServerTime.Now, ip, method, path, statusCode, durationMs, reqBytes, resBytes);
            _requests.Enqueue(m);

            if (Interlocked.Increment(ref _requestCount) > MaxRequests)
            {
                _requests.TryDequeue(out _);
                Interlocked.Decrement(ref _requestCount);
            }
        }

        public void RecordScript(string type, string name, long durationMs, bool success, string? error = null)
        {
            _scripts.Enqueue(new ScriptMetric(ServerTime.Now, type, name, durationMs, success, error));
            if (_scripts.Count > MaxScripts) _scripts.TryDequeue(out _);

            // Update aggregate
            var dict = type switch
            {
                "view" => _viewMetrics,
                "trigger" => _triggerMetrics,
                "queue" => _queueMetrics,
                _ => null
            };

            if (dict != null)
            {
                var agg = dict.GetOrAdd(name, _ => new AggregateMetric());
                Interlocked.Increment(ref agg.CallCount);
                Interlocked.Add(ref agg.TotalMs, durationMs);
                if (!success) Interlocked.Increment(ref agg.ErrorCount);
                agg.LastCalled = ServerTime.Now;

                long cur;
                // MinMs update (spin loop for thread safety without lock)
                do { cur = agg.MinMs; } while (durationMs < cur && Interlocked.CompareExchange(ref agg.MinMs, durationMs, cur) != cur);
                do { cur = agg.MaxMs; } while (durationMs > cur && Interlocked.CompareExchange(ref agg.MaxMs, durationMs, cur) != cur);
            }
        }

        public void RecordSystemSample()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var ramMB = proc.WorkingSet64 / (1024 * 1024);
            _system.Enqueue(new SystemSample(ServerTime.Now, ramMB, 0));
            if (_system.Count > MaxSamples) _system.TryDequeue(out _);
        }

        // ─── Query Methods ────────────────────────────────────────────────────

        public object GetSnapshot(string dataPath)
        {
            var requests = _requests.ToArray();
            var recent = requests.TakeLast(100).ToArray();
            var scripts = _scripts.ToArray();

            // Time series: last 60 requests grouped per minute
            var now = ServerTime.Now;
            var timeSeries = Enumerable.Range(0, 60)
                .Select(i => now.AddMinutes(-59 + i))
                .Select(t => new
                {
                    time = t.ToString("HH:mm"),
                    count = requests.Count(r => r.Timestamp >= t && r.Timestamp < t.AddMinutes(1)),
                    avgMs = requests.Where(r => r.Timestamp >= t && r.Timestamp < t.AddMinutes(1))
                                    .Select(r => r.DurationMs).DefaultIfEmpty(0).Average()
                })
                .ToArray();

            // Disk sizes
            long dbDiskBytes = 0;
            long bulkDiskBytes = 0;
            var collectionSizes = new List<object>();

            if (Directory.Exists(dataPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(dataPath))
                {
                    var name = Path.GetFileName(dir);
                    var size = new DirectoryInfo(dir).EnumerateFiles("*.json", SearchOption.TopDirectoryOnly).Sum(f => f.Length);
                    dbDiskBytes += size;
                    collectionSizes.Add(new { name, sizeKB = size / 1024 });
                }

                var bulkPath = Path.Combine(dataPath, "Bulk");
                if (Directory.Exists(bulkPath))
                    bulkDiskBytes = new DirectoryInfo(bulkPath).EnumerateFiles("*.jsonl").Sum(f => f.Length);
            }

            var proc = System.Diagnostics.Process.GetCurrentProcess();

            return new
            {
                summary = new
                {
                    totalRequests = requests.Length,
                    errorsLast100 = recent.Count(r => r.StatusCode >= 400),
                    avgResponseMs = recent.Length > 0 ? recent.Average(r => r.DurationMs) : 0,
                    ramMB = proc.WorkingSet64 / (1024 * 1024),
                    dbDiskKB = dbDiskBytes / 1024,
                    bulkDiskKB = bulkDiskBytes / 1024,
                    uptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime()).TotalMinutes
                },
                timeSeries,
                recentRequests = recent.Reverse().Take(50).Select(r => new
                {
                    time = r.Timestamp.ToString("HH:mm:ss"),
                    ip = r.Ip,
                    method = r.Method,
                    path = r.Path,
                    status = r.StatusCode,
                    ms = r.DurationMs
                }),
                collectionSizes,
                views = _viewMetrics.Select(kv => new
                {
                    name = kv.Key,
                    calls = kv.Value.CallCount,
                    avgMs = kv.Value.CallCount > 0 ? kv.Value.TotalMs / kv.Value.CallCount : 0,
                    minMs = kv.Value.MinMs == long.MaxValue ? 0 : kv.Value.MinMs,
                    maxMs = kv.Value.MaxMs,
                    errors = kv.Value.ErrorCount,
                    lastCalled = kv.Value.LastCalled
                }),
                triggers = _triggerMetrics.Select(kv => new
                {
                    name = kv.Key,
                    calls = kv.Value.CallCount,
                    avgMs = kv.Value.CallCount > 0 ? kv.Value.TotalMs / kv.Value.CallCount : 0,
                    minMs = kv.Value.MinMs == long.MaxValue ? 0 : kv.Value.MinMs,
                    maxMs = kv.Value.MaxMs,
                    errors = kv.Value.ErrorCount,
                    lastCalled = kv.Value.LastCalled
                }),
                queues = _queueMetrics.Select(kv => new
                {
                    name = kv.Key,
                    calls = kv.Value.CallCount,
                    avgMs = kv.Value.CallCount > 0 ? kv.Value.TotalMs / kv.Value.CallCount : 0,
                    minMs = kv.Value.MinMs == long.MaxValue ? 0 : kv.Value.MinMs,
                    maxMs = kv.Value.MaxMs,
                    errors = kv.Value.ErrorCount,
                    lastCalled = kv.Value.LastCalled
                }),
                systemSamples = _system.TakeLast(60).Select(s => new
                {
                    time = s.Timestamp.ToString("HH:mm"),
                    ramMB = s.RamMB
                })
            };
        }

        public object GetMemorySnapshot(AxarDB.Bridges.MemoryStore memoryStore)
        {
            // MemoryStore doesn't expose collection list directly, 
            // so this is populated via a separate endpoint
            return new { message = "use /memory/list endpoint" };
        }
    }
}
