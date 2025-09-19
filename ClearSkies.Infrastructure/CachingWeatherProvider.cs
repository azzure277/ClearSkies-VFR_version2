using Microsoft.Extensions.Caching.Memory;
// using Microsoft.AspNetCore.Http; // Temporarily removed to unblock build
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
using ClearSkies.Domain.Options;

namespace ClearSkies.Infrastructure
{
    public class CachingWeatherProvider : IMetarSource
    {
        private readonly IMemoryCache _cache;
        private readonly IWeatherProvider _inner;
        private readonly WeatherOptions _options;
        // private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Func<DateTime> _clock;

        public CachingWeatherProvider(
            IMemoryCache cache,
            IWeatherProvider inner,
            Microsoft.Extensions.Options.IOptions<WeatherOptions> options,
            /*IHttpContextAccessor httpContextAccessor,*/
            Func<DateTime>? clock = null)
        {
            _cache = cache;
            _inner = inner;
            _options = options.Value;
            // _httpContextAccessor = httpContextAccessor;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
        {
            var now = _clock();
            var normalizedIcao = icao.Trim().ToUpperInvariant();
            var cacheKey = $"metar:{normalizedIcao}";
            _cache.TryGetValue<Metar>(cacheKey, out var cached);
            // httpContext logic removed for build

            // If cache HIT and fresh, return cached
            if (cached != null)
            {
                var ageMinutes = (now - cached.Observed).TotalMinutes;
                if (ageMinutes <= _options.CacheMinutes)
                {
                    // if (httpContext != null)
                    //     httpContext.Response.Headers["X-Cache-Present"] = "true";
                    cached.RawMetar += "|X-Cache-Present:true";
                    return cached;
                }
            }

            // Otherwise, call upstream (now via IWeatherProvider)
            var upstreamResult = await _inner.GetMetarAsync(icao, ct);
            if (upstreamResult != null)
            {
                _cache.Set(cacheKey, upstreamResult, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes)
                });
                // if (httpContext != null)
                //     httpContext.Response.Headers["X-Cache-Present"] = "false";
                upstreamResult.RawMetar += "|X-Cache-Present:false";
                return upstreamResult;
            }

            // Upstream failed, check for stale (Observed is the timestamp)
            if (cached != null)
            {
                var ageMinutes = (now - cached.Observed).TotalMinutes;
                var maxStale = _options.ServeStaleUpToMinutes > 0 ? _options.ServeStaleUpToMinutes : _options.CacheMinutes;
                if (ageMinutes <= maxStale)
                {
                    // Serve stale, set Warning 110 and cache hit header
                    // if (httpContext != null)
                    // {
                    //     httpContext.Response.Headers["X-Cache-Present"] = "true";
                    //     httpContext.Response.Headers["Warning"] = "110 - Response is stale";
                    //     httpContext.Response.StatusCode = 200;
                    // }
                    return cached;
                }
                // Too old: remove and treat as miss
                _cache.Remove(cacheKey);
            }
            // No valid stale, treat as miss (simulate 5xx)
        // if (httpContext != null)
        // {
        //     httpContext.Response.Headers["X-Cache-Present"] = "false";
        //     httpContext.Response.Headers.Remove("Warning");
        //     httpContext.Response.StatusCode = 503;
        // }
            return null;
        }
    }
}
