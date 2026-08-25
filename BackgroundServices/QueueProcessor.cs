using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using AxarDB;
using AxarDB.Core;
using AxarDB.Definitions;
using System.Text;

namespace AxarDB.BackgroundServices
{
    public class QueueProcessor : BackgroundService
    {
        private readonly DatabaseEngine _db;
        private readonly TimeSpan _pollInterval;

        public QueueProcessor(DatabaseEngine db)
        {
            _db = db;
            _pollInterval = TimeSpan.FromSeconds(db.Settings.QueuePollIntervalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            AxarDB.Logging.Logger.LogDebug("QueueProcessor Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ProcessNextJob(stoppingToken);
                }
                catch (Exception ex)
                {
                    AxarDB.Logging.Logger.LogError($"[QueueProcessor] Critical Error: {ex.Message}");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        private void ProcessNextJob(CancellationToken stoppingToken)
        {
            var sysqueue = _db.GetCollection("sysqueue");
            if (sysqueue == null) return;

            // Find pending jobs: executionTime is null
            // Sort by Priority (desc) then CreatedAt (asc)
            // Note: AxarDB's FindAll returns unsorted list, we sort in memory.
            var pendingJobs = sysqueue.FindAll(d => !d.ContainsKey("executionTime") || d["executionTime"] == null)
                                      .OrderByDescending(d => d.ContainsKey("priority") ? Convert.ToInt32(d["priority"]) : 0)
                                      .ThenBy(d => d.ContainsKey("createdAt") ? Convert.ToDateTime(d["createdAt"]) : DateTime.MaxValue)
                                      .ToList();

            if (pendingJobs.Count == 0) return;

            var job = pendingJobs.First();
            string jobId = job["_id"]?.ToString() ?? "";
            
            // Mark as started
            job["executionTime"] = ServerTime.Now;
            sysqueue.Update(d => d["_id"].ToString() == jobId, job);

            ExecuteJob(job, sysqueue, stoppingToken);
        }

        private void ExecuteJob(Dictionary<string, object> job, Collection sysqueue, CancellationToken stoppingToken)
        {
            string jobId = job["_id"]?.ToString() ?? "unknown";
            string template = job["queryTemplate"]?.ToString() ?? "";
            object? parameters = job.ContainsKey("parameters") ? job["parameters"] : null;
            
            DateTime startTime = ServerTime.Now;
            
            AxarDB.Logging.Logger.LogDebug($"[Queue] Executing Job {jobId}");

            bool success = false;
            string? error = null;
            object? result = null;

            try
            {
                // Create a secure script context
                var scriptContext = new ScriptContext 
                { 
                    IpAddress = "127.0.0.1", 
                    User = "system_queue", 
                    IsView = false 
                };

                // FOR AI TEST NOTES
                // Prepare parameters dictionary for ExecuteScript
                // The parameters in job are stored as List/Array typically if passed as array from JS
                // But ExecuteScript expects Dictionary<string,object> for @param replacement
                // Wait, requirements say: "parameters (array, optional) ... bind to template".
                // AxarDB's current ExecuteScript takes Dictionary<string,object> parameters and replaces @key.
                // If the input is an ARRAY, we might need positional arguments? 
                // "parameters (array, optional) ... An ordered parameter array that will be safely bound"
                // BUT AxarDB ExecuteScript uses NAMED parameters (@key).
                // Let's assume the user passes a Dictionary (Object in JS) for named params, OR if Array, we might map to @p0, @p1?
                // `queue("db.users.insert({name: @name})", {name: "QueueUser"})` -> this is an object/map.
                // Documentation says "parameters (array, optional)". This contradicts usage if typical usage is named.
                // However, "An ordered parameter array" suggests positional. 
                // Let's support BOTH. If it's a Dictionary (JObject), pass as is. 
                // If it's a JArray, maybe we don't support positional yet in ExecuteScript?
                // ExecuteScript Logic: foreach (var param in parameters) script = script.Replace("@" + param.Key ...
                // So it MUST be a Dictionary<string, object>.
                // I will cast parameters to Dictionary<string, object>.
                
                System.Collections.Generic.IDictionary<string, object>? scriptParams = null;
                
                if (parameters is Dictionary<string, object> dictParams)
                {
                    scriptParams = dictParams;
                }
                else if (parameters is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                   try {
                       var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                       scriptParams = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(json) as System.Collections.Generic.IDictionary<string, object>;
                   } catch {}
                }
                else if (parameters != null)
                {
                     try {
                         var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                         scriptParams = AxarDB.Helpers.ScriptUtils.SafeDeserializeJson(json) as System.Collections.Generic.IDictionary<string, object>;
                     } catch {
                     }
                }

                // Execute
                // Reliance on Jint internal timeout and cancellation
                result = _db.ExecuteScript(template, scriptParams, scriptContext, stoppingToken);
                success = true;
            }
            catch (OperationCanceledException)
            {
                error = "QUEUE_CANCELLED";
            }
            catch (TimeoutException)
            {
                error = "QUEUE_TIMEOUT";
            }

            catch (AggregateException ae)
            {
                error = ae.InnerException?.Message ?? ae.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            DateTime endTime = ServerTime.Now;
            long durationMs = (long)(endTime - startTime).TotalMilliseconds;

            // Update Job Record
            job["duration"] = durationMs;
            job["completedAt"] = endTime;
            if (success)
            {
                job["successResult"] = result!;
                job["errorMessage"] = null!;
            }
            else
            {
                job["successResult"] = null!;
                job["errorMessage"] = error!;
            }
            
            // Persist update
            sysqueue.Update(d => d["_id"].ToString() == jobId, job);

            // Log to queue_logs
            LogExecution(jobId, startTime, endTime, durationMs, success, error, result);

            // Record Queue Metric
            AxarDB.Metrics.MetricsCollector.Instance.RecordScript("queue", template.Length > 30 ? template.Substring(0, 30) + "..." : template, durationMs, success, error);
        }

        private void LogExecution(string jobId, DateTime start, DateTime end, long duration, bool success, string? error, object? result)
        {
             try
             {
                 var logEntry = new
                 {
                     jobId,
                     startTime = start,
                     endTime = end,
                     duration,
                     status = success ? "success" : (error == "QUEUE_TIMEOUT" ? "timeout" : "failed"),
                     error,
                     result = success ? (result is string ? result : "Object") : null
                 };

                 string json = System.Text.Json.JsonSerializer.Serialize(logEntry, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                 string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "queue_logs");
                 if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                 
                 File.WriteAllText(Path.Combine(path, $"{jobId}_{ServerTime.Now.Ticks}.json"), json, Encoding.UTF8);
             }
             catch (Exception ex)
             {
                 AxarDB.Logging.Logger.LogError($"[QueueProcessor] Log Error: {ex.Message}");
             }
        }
    }
}
