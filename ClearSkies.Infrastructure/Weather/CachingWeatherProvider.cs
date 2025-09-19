using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClearSkies.Domain;
using ClearSkies.Domain.Options;

namespace ClearSkies.Infrastructure.Weather
{
    // Decorates your existing IWeatherProvider (the one that fetches METARs)
    public sealed class CachingWeatherProvider : IWeatherProvider
    {
    // TODO: Restore caching logic when ready. For now, this is a pass-through provider.
    private readonly IWeatherProvider _inner;
    private readonly Func<DateTime> _clock;

        public CachingWeatherProvider(
            IWeatherProvider inner,
            Func<DateTime>? clock = null)
        {
            _inner = inner;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            // TODO: Caching is temporarily disabled. This provider is a pass-through.
            var metar = await _inner.GetMetarAsync(icao, ct);
            if (metar is null) return null;
            // Optionally keep deterministic timestamps
            // metar.Observed = _clock();
            metar.CacheResult = "MISS";
            // metar.IsStale = false; // Property does not exist
            return metar;
        }

    // CloneMetar not needed while caching is disabled
    }
}
