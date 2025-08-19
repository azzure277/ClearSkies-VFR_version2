
using ClearSkies.Domain.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClearSkies.Domain;

namespace ClearSkies.Infrastructure.Weather
{
    // Decorates your existing IWeatherProvider (the one that fetches METARs)
    public sealed class CachingWeatherProvider : IWeatherProvider
    {
    private readonly IMemoryCache _cache;
    private readonly IWeatherProvider _inner;
    private readonly WeatherOptions _opt;
    private readonly ILogger<CachingWeatherProvider> _log;
    private readonly ClearSkies.Domain.Diagnostics.ICacheStamp _stamp;

        public CachingWeatherProvider(
            IMemoryCache cache,
            IWeatherProvider inner,
            IOptions<WeatherOptions> opt,
            ILogger<CachingWeatherProvider> log,
            ClearSkies.Domain.Diagnostics.ICacheStamp stamp)
        {
            _cache = cache;
            _inner = inner;
            _opt = opt.Value;
            _log = log;
            _stamp = stamp;
        }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            var key = $"metar:{icao.ToUpperInvariant()}";
            if (_cache.TryGetValue(key, out Metar? hit) && hit is not null)
            {
                _stamp.Result = "HIT";
                _log.LogInformation("CACHE HIT {Key}", key);
                return hit;
            }

            var metar = await _inner.GetMetarAsync(icao, ct);
            if (metar is not null)
            {
                _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.CacheMinutes));
            }
            _stamp.Result = "MISS";
            _log.LogInformation("CACHE MISS {Key}", key);
            return metar;
        }
    }
}
