using System.Text.Json;
using AxarDB.Definitions;

namespace AxarDB.Logging
{
    public static class Logger
    {
        private static readonly string RequestLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "request_logs");
        private static readonly string ErrorLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_logs");
        private static readonly string DebugLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_logs");
        private static readonly object _lock = new object();

        static Logger()
        {
            if (!Directory.Exists(RequestLogsPath)) Directory.CreateDirectory(RequestLogsPath);
            if (!Directory.Exists(ErrorLogsPath)) Directory.CreateDirectory(ErrorLogsPath);
            if (!Directory.Exists(DebugLogsPath)) Directory.CreateDirectory(DebugLogsPath);
        }

        public static void LogRequest(string ip, string user, string requestJson, long durationMs, bool success, string errorMessage = "")
        {
            try
            {
                var fileName = ServerTime.Now.ToString("yyyy-MM-dd") + ".txt";
                var filePath = Path.Combine(RequestLogsPath, fileName);
                
                // Log format: [timestamp] - [client ip] - [db user] - [request json, single-line trimmed] - [duration in ms] - [Success or Failed: <reason>]
                var timestamp = ServerTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var status = success ? "Success" : $"Failed: {errorMessage}";
                var cleanedJson = requestJson?.Replace("\r", "").Replace("\n", "").Trim() ?? "";
                
                var logLine = $"[{timestamp}] - [{ip}] - [{user}] - [{cleanedJson}] - [{durationMs}ms] - [{status}]";
                
                lock (_lock)
                {
                    File.AppendAllLines(filePath, new[] { logLine }, System.Text.Encoding.UTF8);
                }
            }
            catch
            {
                // Fallback to console if file logging fails to avoid crashing
                Console.WriteLine("CRITICAL: Failed to write request log.");
            }
        }

        public static void LogError(string message)
        {
            try
            {
                var fileName = ServerTime.Now.ToString("yyyy-MM-dd") + ".txt";
                var filePath = Path.Combine(ErrorLogsPath, fileName);
                
                var timestamp = ServerTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                
                lock (_lock)
                {
                    File.AppendAllLines(filePath, new[] { logLine }, System.Text.Encoding.UTF8);
                }
            }
            catch
            {
                // Fallback to console if file logging fails to avoid crashing
                Console.WriteLine("CRITICAL: Failed to write error log.");
            }
        }

        public static void LogDebug(string message)
        {
            try
            {
                var fileName = ServerTime.Now.ToString("yyyy-MM-dd") + ".txt";
                var filePath = Path.Combine(DebugLogsPath, fileName);
                
                var timestamp = ServerTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] [DEBUG] {message}";
                
                lock (_lock)
                {
                    File.AppendAllLines(filePath, new[] { logLine }, System.Text.Encoding.UTF8);
                }

                // Still write to console if in Debug build or environment, 
                // but let's keep it strictly to file for now as per user request to clean up.
                // Console.WriteLine($"[DEBUG] {message}");
            }
            catch
            {
                // Silence debug logging errors to console to avoid clutter
            }
        }
    }
}
