
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
    if (_cache.TryGetValue(key, out Metar? hit) && hit != null)
    {
        var now = _clock();
        var ageMinutes = (now - hit.Observed).TotalMinutes;
        if (ageMinutes <= _opt.Value.CacheMinutes)
        {
            var ctx = _httpContextAccessor?.HttpContext;
            if (ctx?.Response?.Headers != null)
            {
                ctx.Response.Headers["X-Cache-Present"] = "true";
                ctx.Response.Headers.Remove("Warning"); // Clear Warning header if present
            }
            return hit;
        }
        // Don't remove cache entry yet; allow stale-on-error fallback
    }

        Metar? metar = null;
        try
        {
            metar = await _inner.GetLatestAsync(icao, ct);
        }
        catch
        {
            // Upstream failed, check for stale
            if (_cache.TryGetValue(key, out Metar? stale) && stale != null)
            {
                var now = _clock();
                var ageMinutes = (now - stale.Observed).TotalMinutes;
                var maxStale = _opt.Value.ServeStaleUpToMinutes ?? 5;
                if (ageMinutes > _opt.Value.CacheMinutes && ageMinutes <= maxStale)
                {
                    var ctx = _httpContextAccessor?.HttpContext;
                    if (ctx?.Response?.Headers != null)
                    {
                        ctx.Response.Headers["Warning"] = "110 - Response is Stale";
                        ctx.Response.Headers["X-Cache-Present"] = "true";
                    }
                    return stale;
                }
                // If too old, remove from cache
                _cache.Remove(key);
            }
            // No valid stale, treat as miss
            var missCtx2 = _httpContextAccessor?.HttpContext;
            if (missCtx2?.Response?.Headers != null)
            {
                missCtx2.Response.Headers["X-Cache-Present"] = "false";
                missCtx2.Response.Headers.Remove("Warning"); // Clear Warning header if present
            }
            return null;
        }

        if (metar != null)
        {
            _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
            var missCtx = _httpContextAccessor?.HttpContext;
            if (missCtx?.Response?.Headers != null)
            {
                missCtx.Response.Headers["X-Cache-Present"] = "false";
                missCtx.Response.Headers.Remove("Warning"); // Clear Warning header if present
            }
            return metar;
        }

        // Upstream returned null, check for stale
        if (_cache.TryGetValue(key, out Metar? stale2))
        if (_cache.TryGetValue(key, out Metar? fallback) && fallback != null)
        {
            var now = _clock();
            var ageMinutes = (now - fallback.Observed).TotalMinutes;
            var maxStale = _opt.Value.ServeStaleUpToMinutes ?? 5;
            if (ageMinutes <= maxStale)
            {
                var ctx = _httpContextAccessor?.HttpContext;
                if (ctx?.Response?.Headers != null)
                {
                    ctx.Response.Headers["Warning"] = "110 - Response is Stale";
                    ctx.Response.Headers["X-Cache-Present"] = "true";
                }
                return fallback;
            }
            // If too old, remove from cache
            _cache.Remove(key);
        }
        var missCtx3 = _httpContextAccessor?.HttpContext;
        if (missCtx3?.Response?.Headers != null)
        {
            missCtx3.Response.Headers["X-Cache-Present"] = "false";
            missCtx3.Response.Headers.Remove("Warning"); // Clear Warning header if present
        }
        return null;
    }
}
