using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AxarDB.Sdk
{
    public class AxarRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public AxarRateLimiter(IMemoryCache cache, ILogger logger = null)
        {
            _cache = cache;
            _logger = logger;
        }

        public bool CheckLimit(string key, string durationStr, string type, string condition = null)
        {
            var duration = ParseDuration(durationStr);
            var cacheKey = $"ratelimit:{type}:{key}:{condition ?? "default"}";

            // For simplicity in this client-side implementation, we are just tracking existence 
            // or count. The requirement implies checking if the limit IS REACHED.
            // Since the user didn't specify *how many* requests are allowed in the prompt details 
            // (e.g. "100 requests per hour"), but asked for a "tracking mechanism", 
            // we will implement a counter. 
            // However, the prompt says: "When restriction occurs... log details".
            // This implies the SDK needs to know the LIMIT.
            // But the method signature provided is: WithRateLimit("192.168.2.1", "1h", "ip_ratelimit", "warning")
            // It lacks the "count limit" (e.g. 100).
            // I will assume the limit is configured elsewhere or passed in the constructor/config? 
            // Or maybe this function just *records* the hit and we need another method to set the policy?
            // "The application will follow a rate limit system... optional... When restricted, log info."
            
            // It seems "WithRateLimit" might be generating the restriction KEY and passing it to the server?
            // NO, the SDK tracks this entirely in client-side memory. It's client-side.
            // But where is the LIMIT defined? 
            // I'll add a separate configuration method/dictionary to map "type" -> "maxRequests".
            // For now, I'll default to 100 if not specified, or allow configuration.

            // Let's obtain the limit for this 'type'.
            int limit = GetLimitForType(type); 

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = duration;
                return new RateLimitCounter { Count = 0, Limit = limit };
            }).Increment() > limit;
        }
        
        // This dictionary would ideally be populated via configuration
        private System.Collections.Concurrent.ConcurrentDictionary<string, int> _limits = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        public void SetLimit(string type, int maxRequests)
        {
            _limits[type] = maxRequests;
        }

        private int GetLimitForType(string type)
        {
            return _limits.TryGetValue(type, out int limit) ? limit : 1000; // Default buffer
        }

        public void LogRestriction(string key, string duration, string type, string condition)
        {
            _logger?.LogWarning("Rate limit exceeded for {Type} on {Key}. Duration: {Duration}, Condition: {Condition}", 
                type, key, duration, condition);
        }

        private TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration)) return TimeSpan.Zero;
            var unit = duration[duration.Length - 1];
            if (double.TryParse(duration.Substring(0, duration.Length - 1), out double value))
            {
                 switch (char.ToLowerInvariant(unit))
                 {
                     case 's': return TimeSpan.FromSeconds(value);
                     case 'm': return TimeSpan.FromMinutes(value);
                     case 'h': return TimeSpan.FromHours(value);
                     case 'd': return TimeSpan.FromDays(value);
                 }
            }
            return TimeSpan.FromMinutes(1); // Default fallack
        }

        private class RateLimitCounter
        {
            public int Count;
            public int Limit;
            public int Increment() => System.Threading.Interlocked.Increment(ref Count);
        }
    }
}
