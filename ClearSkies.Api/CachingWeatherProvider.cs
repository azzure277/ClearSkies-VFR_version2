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
    private readonly IOptions<WeatherOptions> _opt;

    public CachingWeatherProvider(IMemoryCache cache, IWeatherProvider inner, IOptions<WeatherOptions> opt)
    { _cache = cache; _inner = inner; _opt = opt; }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        var key = $"metar:{icao}";
        if (_cache.TryGetValue(key, out Metar? hit)) return hit;

        var metar = await _inner.GetMetarAsync(icao, ct);
        _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
        return metar;
    }
}
