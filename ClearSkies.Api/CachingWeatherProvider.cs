
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;


public sealed class CachingWeatherProvider : IMetarSource
{
    private readonly IMemoryCache _cache;
    private readonly IMetarSource _inner;
    private readonly IOptions<WeatherOptions> _opt;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly Func<DateTime> _clock;

    public CachingWeatherProvider(
        IMemoryCache cache,
        IMetarSource inner,
        IOptions<WeatherOptions> opt,
        IHttpContextAccessor? httpContextAccessor = null,
        Func<DateTime>? clock = null)
    {
        _cache = cache;
        _inner = inner;
        _opt = opt;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
    {
        var key = $"metar:{icao.Trim().ToUpperInvariant()}";
        if (_cache.TryGetValue(key, out Metar? hit))
        {
            var now = _clock();
            var ageMinutes = (now - hit.Observed).TotalMinutes;
            if (ageMinutes <= _opt.Value.CacheMinutes)
            {
                // Set header for cache hit
                var ctx = _httpContextAccessor?.HttpContext;
                if (ctx?.Response?.Headers != null)
                    ctx.Response.Headers["X-Cache-Present"] = "true";
                return hit;
            }
            // Too old: remove and treat as miss
            _cache.Remove(key);
        }

        var metar = await _inner.GetLatestAsync(icao, ct);
        if (metar != null)
        {
            _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
            // Set header for cache miss
            var missCtx = _httpContextAccessor?.HttpContext;
            if (missCtx?.Response?.Headers != null)
                missCtx.Response.Headers["X-Cache-Present"] = "false";
            return metar;
        }

        // Upstream failed, check for stale
        if (_cache.TryGetValue(key, out Metar? stale))
        {
            var now = _clock();
            var ageMinutes = (now - stale.Observed).TotalMinutes;
            if (ageMinutes <= _opt.Value.CacheMinutes)
            {
                var ctx = _httpContextAccessor?.HttpContext;
                if (ctx?.Response?.Headers != null)
                    ctx.Response.Headers["X-Cache-Present"] = "true";
                return stale;
            }
            // If too old, remove from cache
            _cache.Remove(key);
        }
        // No valid stale, treat as miss
        var missCtx2 = _httpContextAccessor?.HttpContext;
        if (missCtx2?.Response?.Headers != null)
            missCtx2.Response.Headers["X-Cache-Present"] = "false";
        return null;
    }
}
