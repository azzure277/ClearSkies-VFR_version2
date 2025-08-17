using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
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

        public CachingWeatherProvider(
            IMemoryCache cache,
            IWeatherProvider inner,
            IOptions<WeatherOptions> opt)
        {
            _cache = cache;
            _inner = inner;
            _opt = opt.Value;
        }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            var key = $"metar:{icao.ToUpperInvariant()}";

            if (_cache.TryGetValue(key, out Metar? hit) && hit is not null)
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
