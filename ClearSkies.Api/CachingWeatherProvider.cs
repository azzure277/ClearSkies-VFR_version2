
using ClearSkies.Domain.Options;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

public sealed class CachingWeatherProvider : IWeatherProvider
{
    private readonly IMemoryCache _cache;
    private readonly IWeatherProvider _inner;
    private readonly WeatherOptions _opt;
    private readonly ClearSkies.Domain.Diagnostics.ICacheStamp _stamp;
    private readonly Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider> _log;

    public CachingWeatherProvider(
        IMemoryCache cache,
        IWeatherProvider inner,
        IOptions<WeatherOptions> opt,
        ClearSkies.Domain.Diagnostics.ICacheStamp stamp,
        Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider> log)
    {
        _cache = cache;
        _inner = inner;
        _opt = opt.Value;
        _stamp = stamp;
        _log = log;
    }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        var code = icao.ToUpperInvariant();
        var key = $"metar:{code}";
        var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        // 1. Check cache first
        if (_cache.TryGetValue(key, out Metar? cached) && cached is not null)
        {
            _stamp.Result = "HIT";
            _log.LogInformation($"[PID {pid}] CACHE HIT {key}");
            return cached;
        }

        // 2. Try to fetch fresh
        try
        {
            var fresh = await _inner.GetMetarAsync(code, ct);
            if (fresh is null)
            {
                _stamp.Result = "MISS";
                _log.LogInformation($"[PID {pid}] CACHE MISS (no data) {key}");
                return null;
            }

            _cache.Set(key, fresh, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _opt.CacheMinutes))
            });

            _stamp.Result = "MISS";
            _log.LogInformation($"[PID {pid}] CACHE MISS -> stored {key}");
            return fresh;
        }
        catch (Exception ex) when (_opt.ServeStaleUpToMinutes > 0)
        {
            // 3. On error, try to serve fallback from cache
            if (_cache.TryGetValue(key, out Metar? stale) && stale is not null)
            {
                var ageMin = (int)Math.Round((DateTime.UtcNow - stale.Observed).TotalMinutes);
                if (ageMin <= _opt.ServeStaleUpToMinutes)
                {
                    _stamp.Result = "FALLBACK";
                    _log.LogWarning(ex, $"[PID {pid}] Upstream failed; serving FALLBACK for {code} (age={ageMin}m) {key}");
                    return stale;
                }
            }

            _stamp.Result = "MISS";
            _log.LogError(ex, $"[PID {pid}] Upstream failed; no fallback available for {code} {key}");
            return null;
        }
    }
}
