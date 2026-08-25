using System;
using System.IO;
using System.Text;
using AxarDB.Logging;
using AxarDB.Definitions;

namespace AxarDB.Core
{
    public static class BackupQuery
    {
        private static readonly object _lock = new object();
        public static System.Threading.AsyncLocal<string?> CurrentUser = new System.Threading.AsyncLocal<string?>();

        /// <summary>
        /// Logs a recovery query to the daily backup_queries file.
        /// </summary>
        public static void LogRecoveryQuery(string basePath, string query)
        {
            try
            {
                var dir = Path.Combine(basePath, "backup_queries");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var fileName = $"{ServerTime.Now:yyyy-MM-dd}.txt";
                var path = Path.Combine(dir, fileName);

                lock (_lock)
                {
                    string timestamp = ServerTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string user = CurrentUser.Value ?? "System";
                    string comment = $"// [{timestamp}] User: {user}";
                    
                    File.AppendAllText(path, comment + Environment.NewLine + query + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BackupQuery Error] Failed to write backup query: {ex.Message}");
            }
        }
    }
}
