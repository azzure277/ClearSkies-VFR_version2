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
    private readonly ClearSkies.Domain.Diagnostics.ICacheStamp _stamp;
    private readonly Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider> _logger;

    public CachingWeatherProvider(IMemoryCache cache, IWeatherProvider inner, IOptions<WeatherOptions> opt, ClearSkies.Domain.Diagnostics.ICacheStamp stamp)
    {
        _cache = cache;
        _inner = inner;
        _opt = opt;
        _stamp = stamp;
        _logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CachingWeatherProvider>();
    }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        var key = $"metar:{icao}";
        if (_cache.TryGetValue(key, out Metar? hit))
        {
            _stamp.Result = "HIT";
            _logger.LogInformation($"Cache HIT for {icao}, stamp.Result={_stamp.Result}");
            return hit;
        }

        var metar = await _inner.GetMetarAsync(icao, ct);
        _cache.Set(key, metar, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
        _stamp.Result = "MISS";
        _logger.LogInformation($"Cache MISS for {icao}, stamp.Result={_stamp.Result}");
        return metar;
    }
}
