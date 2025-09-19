
// ...existing code...
// ...existing code...

using ClearSkies.Domain.Options;
using ClearSkies.Domain;
// ...existing code...
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace ClearSkies.Api
{

    public sealed class CachingWeatherProvider : IWeatherProvider
    {
    private readonly IMemoryCache _cache;
    private readonly IWeatherProvider _inner;
    private readonly WeatherOptions _opt;
    private readonly ClearSkies.Domain.Diagnostics.ICacheStamp _stamp;
    private readonly Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider> _log;
    private readonly Func<DateTime> _clock;

        public CachingWeatherProvider(
            IMemoryCache cache,
            IWeatherProvider inner,
            IOptions<WeatherOptions> opt,
            ClearSkies.Domain.Diagnostics.ICacheStamp stamp,
            Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider> log,
            Func<DateTime>? clock = null)
        {
            _cache = cache;
            _inner = inner;
            _opt = opt.Value;
            _stamp = stamp;
            _log = log;
            _clock = clock ?? (() => DateTime.UtcNow);
            _log.LogWarning($"[DIAGNOSTIC] ClearSkies.Api.CachingWeatherProvider constructed. CacheMinutes={_opt.CacheMinutes}, ServeStaleUpToMinutes={_opt.ServeStaleUpToMinutes}");
        }
    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            var code = icao.ToUpperInvariant();
            var key = $"metar:{code}";
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var cacheHash = _cache.GetHashCode();
            var now = _clock();
            _log.LogWarning($"[DIAGNOSTIC] Cache access: key={key}, cacheHash={cacheHash}, time={now:O}");
            if (_cache.TryGetValue(key, out Metar? debugCached) && debugCached is not null)
            {
                _log.LogDebug($"[PID {pid}] Cache contains: Observed={debugCached.Observed:O}, Age={(now - debugCached.Observed).TotalMinutes:F2} min");
            }

            // 1. Check cache first, but only return HIT if entry is fresh (within CacheMinutes)
            if (_cache.TryGetValue(key, out Metar? cached) && cached is not null)
            {
                var ageMin = (now - cached.Observed).TotalMinutes;
                _log.LogDebug($"[PID {pid}] Cache HIT check: Observed={cached.Observed:O}, Age={ageMin:F2} min, CacheMinutes={_opt.CacheMinutes}");
                if (ageMin < _opt.CacheMinutes)
                {
                    _stamp.Result = "HIT";
                    // Manually clone Metar with CacheResult=HIT
                    var hit = new Metar(
                        cached.Icao,
                        cached.Observed,
                        cached.WindDirDeg,
                        cached.WindKt,
                        cached.GustKt,
                        cached.VisibilitySm,
                        cached.CeilingFtAgl,
                        cached.TemperatureC,
                        cached.DewpointC,
                        cached.AltimeterInHg
                    );
                    hit.RawMetar = cached.RawMetar;
                    hit.CacheResult = "HIT";
                    _log.LogInformation($"[PID {pid}] CACHE HIT {key}");
                    return hit;
                }
                _log.LogDebug($"[PID {pid}] Cache STALE for {key}, age={ageMin:F2} min");
                // else: stale, try refresh below
            }

            // Only fetch/store if cache is missing or stale
            try
            {
                var fresh = await _inner.GetMetarAsync(code, ct);
                if (fresh is null)
                {
                    _stamp.Result = "MISS";
                    _log.LogInformation($"[PID {pid}] CACHE MISS (no data) {key}");
                    return null;
                }

                // Store in cache since entry is missing or stale
                _cache.Set(key, fresh, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _opt.CacheMinutes))
                });

                _stamp.Result = "MISS";
                fresh.CacheResult = "MISS";
                _log.LogInformation($"[PID {pid}] CACHE MISS -> stored {key}");
                return fresh;
            }
            catch (Exception ex) when (_opt.ServeStaleUpToMinutes > 0)
            {
                // 3. On error, try to serve fallback from cache
                if (_cache.TryGetValue(key, out Metar? stale) && stale is not null)
                {
                    var ageMin = (now - stale.Observed).TotalMinutes;
                    if (ageMin < _opt.ServeStaleUpToMinutes)
                    {
                        _stamp.Result = "FALLBACK";
                        // Manually clone Metar with CacheResult=FALLBACK
                        var fallback = new Metar(
                            stale.Icao,
                            stale.Observed,
                            stale.WindDirDeg,
                            stale.WindKt,
                            stale.GustKt,
                            stale.VisibilitySm,
                            stale.CeilingFtAgl,
                            stale.TemperatureC,
                            stale.DewpointC,
                            stale.AltimeterInHg
                        );
                        fallback.RawMetar = stale.RawMetar;
                        fallback.CacheResult = "FALLBACK";
                        _log.LogWarning(ex, $"[PID {pid}] Upstream failed; serving FALLBACK for {code} (age={ageMin:F2}m) {key}");
                        return fallback;
                    }
                }

                _stamp.Result = "MISS";
                _log.LogError(ex, $"[PID {pid}] Upstream failed; no fallback available for {code} {key}");
                return null;
            }
        }
    }
}

