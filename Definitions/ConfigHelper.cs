using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AxarDB.Definitions
{
    public static class ConfigHelper
    {
        public static double CurrentTimezoneOffset { get; set; } = 3.0;

        public static DatabaseSettings LoadSettings(string? targetPath)
        {
            var basePath = targetPath ?? AppDomain.CurrentDomain.BaseDirectory;
            var sysconfigDir = Path.Combine(basePath, "Data", "sysconfig");
            var settings = new DatabaseSettings();

            if (Directory.Exists(sysconfigDir))
            {
                try
                {
                    var files = Directory.GetFiles(sysconfigDir, "*.json");
                    if (files.Length > 0)
                    {
                        var content = File.ReadAllText(files[0]);
                        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                        if (dict != null)
                        {
                            if (dict.TryGetValue("memoryLimitPercentage", out var memVal) && memVal != null)
                                settings.MemoryLimitPercentage = Convert.ToDouble(memVal, System.Globalization.CultureInfo.InvariantCulture);
                            
                            if (dict.TryGetValue("bulkStoreMaxCacheBytes", out var bulkVal) && bulkVal != null)
                                settings.BulkStoreMaxCacheBytes = Convert.ToInt64(bulkVal);

                            if (dict.TryGetValue("maxRecursionDepth", out var recVal) && recVal != null)
                                settings.MaxRecursionDepth = Convert.ToInt32(recVal);

                            if (dict.TryGetValue("queryTimeoutMinutes", out var timeoutVal) && timeoutVal != null)
                                settings.QueryTimeoutMinutes = Convert.ToInt32(timeoutVal);

                            if (dict.TryGetValue("queuePollIntervalSeconds", out var pollVal) && pollVal != null)
                                settings.QueuePollIntervalSeconds = Convert.ToDouble(pollVal, System.Globalization.CultureInfo.InvariantCulture);

                            if (dict.TryGetValue("timezoneOffset", out var tzVal) && tzVal != null)
                                settings.TimezoneOffset = Convert.ToDouble(tzVal, System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AxarDB Warning] Failed to load settings from sysconfig, using defaults: {ex.Message}");
                }
            }
            
            CurrentTimezoneOffset = settings.TimezoneOffset;
            return settings;
        }
    }
}
