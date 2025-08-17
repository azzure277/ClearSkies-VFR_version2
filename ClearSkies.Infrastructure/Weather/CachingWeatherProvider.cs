using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClearSkies.Domain.Options;
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

        public CachingWeatherProvider(
            IMemoryCache cache,
            IWeatherProvider inner,
            IOptions<WeatherOptions> opt,
            ILogger<CachingWeatherProvider> log)
        {
            _cache = cache;
            _inner = inner;
            _opt = opt.Value;
            _log = log;
        }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            var key = $"metar:{icao.ToUpperInvariant()}";

            _cache.TryGetValue(key, out Metar? hit);
            _log.LogInformation("CACHE {Result} {Key}", hit is null ? "MISS" : "HIT", key);
            if (hit is not null)
                return hit;

            var metar = await _inner.GetMetarAsync(icao, ct);
            if (metar is not null)
            {
                _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.CacheMinutes));
            }
            return metar;
        }
    }
}
