using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using AxarDB.Core;
using AxarDB.Definitions;
using AxarDB.Extensions;

namespace AxarDB.Extensions
{
    public static class EndpointExtensions
    {
        public static void MapDatabaseEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/collections", (DatabaseEngine dbEngine) => 
            {
                return Results.Json(dbEngine.ExecuteScript("showCollections()"));
            });

            app.MapDelete("/collections/{name}", (string name, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteCollection(name);
                return Results.Ok(new { success = true });
            });

            app.MapPost("/query", async (HttpContext context, DatabaseEngine dbEngine) =>
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                var script = await reader.ReadToEndAsync();
                
                var queryParams = context.Request.Query.ToDictionary(
                    k => k.Key, 
                    v => (object)(v.Value.FirstOrDefault() ?? string.Empty)
                );
                
                try 
                {
                    string user = "anonymous";
                    if (context.TryGetBasicCredentials(out string? username, out _))
                    {
                        user = username;
                    }
                
                    var scriptContext = new ScriptContext 
                    { 
                        IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        User = user,
                        IsView = false
                    };

                    var result = dbEngine.ExecuteScript(script, queryParams, scriptContext, context.RequestAborted);
                    return Results.Json(result);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapGet("/views/{viewName}", async (string viewName, HttpContext context, DatabaseEngine dbEngine) => 
            {
                string access = dbEngine.GetViewAccess(viewName);
                string user = "anonymous";

                if (access == "private")
                {
                    bool authenticated = false;
                    if (context.TryGetBasicCredentials(out string? username, out string? password))
                    {
                        if (dbEngine.Authenticate(username, password))
                        {
                            user = username;
                            authenticated = true;
                        }
                    }

                    if (!authenticated)
                    {
                        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"AxarDB Views\"";
                        return Results.Unauthorized();
                    }
                }
                else
                {
                    user = "public_user";
                }

                var queryParams = context.Request.Query.ToDictionary(
                    k => k.Key, 
                    v => (object)(v.Value.FirstOrDefault() ?? string.Empty)
                );

                try
                {
                    var result = dbEngine.ExecuteView(viewName, queryParams, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", user, context.RequestAborted);
                    return Results.Json(result);
                }
                catch (FileNotFoundException)
                {
                    return Results.NotFound(new { error = $"View '{viewName}' not found" });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapDelete("/views/{viewName}", (string viewName, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteView(viewName);
                return Results.Ok(new { success = true });
            });

            app.MapDelete("/triggers/{triggerName}", (string triggerName, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteTrigger(triggerName);
                return Results.Ok(new { success = true });
            });

            app.MapGet("/memory/list", (DatabaseEngine dbEngine) =>
            {
                var list = dbEngine.MemoryStore.GetCollectionNames()
                    .Select(name => new
                    {
                        name = name,
                        count = dbEngine.MemoryStore.GetRecordCount(name)
                    })
                    .ToList();
                return Results.Json(list);
            });

            app.MapGet("/bulk/list", (DatabaseEngine dbEngine) =>
            {
                var list = dbEngine.BulkStore.ListCollections()
                    .Select(name => {
                        var path = Path.Combine(dbEngine.BasePath, "Bulk", $"{name}.jsonl");
                        var info = new FileInfo(path);
                        return new
                        {
                            name = name,
                            file = $"{name}.jsonl",
                            recordCount = dbEngine.BulkStore.GetDocuments(name).Count(),
                            sizeKB = info.Exists ? info.Length / 1024.0 : 0
                        };
                    })
                    .ToList();
                return Results.Json(list);
            });

            app.MapGet("/metrics", (DatabaseEngine dbEngine) =>
            {
                var snapshot = AxarDB.Metrics.MetricsCollector.Instance.GetSnapshot(Path.Combine(dbEngine.BasePath, "Data"));
                return Results.Json(snapshot);
            });

            app.MapGet("/api/rag-doc", async () =>
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "llm_ragfile_en.md");
                if (!File.Exists(path)) {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "llm_ragfile_en.md");
                }
                if (File.Exists(path))
                {
                    var content = await File.ReadAllTextAsync(path);
                    return Results.Text(content, "text/markdown");
                }
                return Results.NotFound(new { error = "Rag doc not found" });
            });

            app.MapPost("/api/ai-query", async (HttpContext context) =>
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                
                var json = System.Text.Json.JsonDocument.Parse(body);
                var root = json.RootElement;
                
                string apiUrl = root.TryGetProperty("apiUrl", out var urlEl) ? urlEl.GetString() ?? "" : "";
                string modelName = root.TryGetProperty("modelName", out var modEl) ? modEl.GetString() ?? "" : "";
                string apiKey = root.TryGetProperty("apiKey", out var keyEl) ? keyEl.GetString() ?? "" : "";
                string query = root.TryGetProperty("query", out var queryEl) ? queryEl.GetString() ?? "" : "";
                string schemaContext = root.TryGetProperty("schemaContext", out var schemaEl) ? schemaEl.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(apiUrl)) return Results.Problem("API URL is required", statusCode: 400);
                if (string.IsNullOrEmpty(modelName)) return Results.Problem("Model Name is required", statusCode: 400);
                if (string.IsNullOrEmpty(apiKey)) return Results.Problem("API Key is required", statusCode: 400);

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "llm_ragfile_en.md");
                if (!File.Exists(path)) {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "llm_ragfile_en.md");
                }
                string ragContent = "";
                if (File.Exists(path)) {
                    ragContent = await File.ReadAllTextAsync(path);
                }

                var systemPrompt = "You are an expert AxarDB Query assistant. AxarDB is a JavaScript-based in-memory NoSQL database. IT IS NOT MONGODB!\n" +
                                   "CRITICAL RULES:\n" +
                                   "1. NEVER use MongoDB syntax (e.g. { age: { $gt: 18 } }). Queries use JavaScript arrow functions (e.g. x => x.age > 18).\n" +
                                   "2. 'findOne()' DOES NOT EXIST. Use 'find(predicate)' to get a single item, or 'findall(predicate)' to get multiple.\n" +
                                   "3. Read the provided Context carefully and follow its exact syntax.\n" +
                                   "4. Return ONLY the raw JavaScript query code. No markdown formatting, no explanations, no ````javascript` blocks. The user will execute this code directly.\n\n" +
                                   "Database Schema (First record of each collection):\n" + schemaContext + "\n\n" +
                                   "Context:\n" + ragContent;

                var payload = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = query }
                    },
                    temperature = 0.2
                };

                using var httpClient = new System.Net.Http.HttpClient();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }
                
                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await httpClient.PostAsync(apiUrl, content);
                    var responseString = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        return Results.Problem(responseString, statusCode: (int)response.StatusCode);
                    }

                    return Results.Text(responseString, "application/json");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message, statusCode: 500);
                }
            });
        }
    }
}
