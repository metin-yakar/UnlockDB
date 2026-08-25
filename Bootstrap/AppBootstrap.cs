using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AxarDB;
using AxarDB.Core;
using AxarDB.Definitions;
using AxarDB.Diagnostics;
using AxarDB.Logging;
using AxarDB.Middleware;
using AxarDB.Extensions;

namespace AxarDB.Bootstrap
{
    public static class AppBootstrap
    {
        public static void Run(string[] args)
        {
            // Set global culture and encoding
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");
            Console.OutputEncoding = Encoding.UTF8;

            var startupOptions = StartupOptions.Parse(args);
            var settings = ConfigHelper.LoadSettings(startupOptions.TargetPath);

            if (startupOptions.IsScript)
            {
                RunScript(startupOptions, settings);
                return;
            }

            if (startupOptions.IsBenchmark)
            {
                Benchmark.Run();
                return;
            }

            if (startupOptions.IsTestMemory)
            {
                MemoryTest.Run();
                return;
            }

            RunWebHost(args, startupOptions, settings);
        }

        private static void RunScript(StartupOptions startupOptions, DatabaseSettings settings)
        {
            var scriptPath = startupOptions.ScriptPath;
            if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
            {
                var db = new DatabaseEngine(startupOptions.TargetPath, settings);
                try 
                {
                    var content = File.ReadAllText(scriptPath, Encoding.UTF8);
                    var result = db.ExecuteScript(content);
                    Console.WriteLine("Result: " + (result?.ToString() ?? "null"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine($"File not found or missing argument: {scriptPath}");
            }
        }

        private static void RunWebHost(string[] args, StartupOptions startupOptions, DatabaseSettings settings)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
            });

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(startupOptions.Port);
            });

            var dbEngine = new DatabaseEngine(startupOptions.TargetPath, settings);
            builder.Services.AddSingleton(dbEngine);
            builder.Services.AddSingleton(settings);
            builder.Services.AddHostedService<AxarDB.BackgroundServices.QueueProcessor>();

            builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            {
                options.SerializerOptions.Converters.Add(new AxarDB.Helpers.BigIntegerConverter());
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    if (string.IsNullOrEmpty(startupOptions.CorsOrigins) || startupOptions.CorsOrigins == "*")
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    }
                    else
                    {
                        var origins = startupOptions.CorsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        policy.WithOrigins(origins)
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    }
                });
            });

            dbEngine.InitializeTriggers();

            var app = builder.Build();
            app.UseCors();

            // Register custom middlewares
            app.UseGlobalExceptionHandling();
            app.UseRequestLogging();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseBasicAuthentication();

            // Map static redirects
            app.MapGet("/", () => Results.Redirect("/index.html"));
            app.MapGet("/docs", () => Results.Redirect("/docs.html"));
            app.MapGet("/monitor", () => Results.Redirect("/monitoring.html"));

            // Map API endpoints
            app.MapDatabaseEndpoints();

            app.Run();
        }
    }
}
