
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

    public CachingWeatherProvider(
        IMemoryCache cache,
        IMetarSource inner,
        IOptions<WeatherOptions> opt,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _cache = cache;
        _inner = inner;
        _opt = opt;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
    {
        var key = $"metar:{icao.Trim().ToUpperInvariant()}";
        if (_cache.TryGetValue(key, out Metar? hit))
        {
            // Set header for cache hit
            var ctx = _httpContextAccessor?.HttpContext;
            if (ctx?.Response?.Headers != null)
                ctx.Response.Headers["X-Cache-Present"] = "true";
            return hit;
        }

        var metar = await _inner.GetLatestAsync(icao, ct);
        _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
        // Set header for cache miss
        var missCtx = _httpContextAccessor?.HttpContext;
        if (missCtx?.Response?.Headers != null)
            missCtx.Response.Headers["X-Cache-Present"] = "false";
        return metar;
    }
}
