using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using AxarDB.Core;
using AxarDB.Extensions;

namespace AxarDB.Middleware
{
    public class BasicAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly DatabaseEngine _dbEngine;

        public BasicAuthenticationMiddleware(RequestDelegate next, DatabaseEngine dbEngine)
        {
            _next = next;
            _dbEngine = dbEngine;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/query") || 
                context.Request.Path.StartsWithSegments("/collections") ||
                context.Request.Path.StartsWithSegments("/memory/list") ||
                context.Request.Path.StartsWithSegments("/bulk/list") ||
                context.Request.Path.StartsWithSegments("/metrics"))
            {
                if (context.TryGetBasicCredentials(out string? username, out string? password))
                {
                    if (_dbEngine.Authenticate(username, password))
                    {
                        AxarDB.Core.BackupQuery.CurrentUser.Value = username;
                        await _next(context);
                        return;
                    }
                }
                
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"AxarDB\"";
                context.Response.StatusCode = 401;
                return;
            }

            await _next(context);
        }
    }
}
