using System;
using System.Collections.Concurrent;
using System.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using Microsoft.Extensions.Caching.Memory;

using Microsoft.AspNetCore.Http;
using ClearSkies.Domain.Diagnostics;

internal sealed class DeterministicCachingWeatherProvider : IWeatherProvider
{
    private readonly IMemoryCache _cache;
    private readonly IWeatherProvider _inner;
    private readonly IHttpContextAccessor _http;
    private readonly ICacheStamp _stamp;

    public DeterministicCachingWeatherProvider(IMemoryCache cache, IWeatherProvider inner, IHttpContextAccessor http, ICacheStamp stamp)
    {
        _cache = cache;
        _inner = inner;
        _http = http;
        _stamp = stamp;
    }

    public async Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        var safeIcao = icao ?? string.Empty;
        var key = $"metar:{safeIcao.Trim().ToUpperInvariant()}";
        var cacheHash = _cache.GetHashCode();
        Console.WriteLine($"[DeterministicCachingWeatherProvider] cache instance={cacheHash} key='{key}'");
        var presentBefore = _cache.TryGetValue(key, out Metar? cached);
        Console.WriteLine($"[DeterministicCachingWeatherProvider] TryGetValue before: present={presentBefore} key='{key}'");
        _http.HttpContext?.Response.Headers.Append("X-Cache-Present", presentBefore ? "true" : "false");

        if (presentBefore && cached != null)
        {
            _stamp.Result = "HIT";
            Console.WriteLine($"[DeterministicCachingWeatherProvider] HIT for key='{key}'");
            return cached;
        }

        var fresh = await _inner.GetMetarAsync(safeIcao, ct);
        if (fresh is not null)
        {
            _cache.Set(key, fresh, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
            _stamp.Result = "MISS";
            Console.WriteLine($"[DeterministicCachingWeatherProvider] MISS and set key='{key}'");
        }
        else
        {
            _stamp.Result = "NULL";
            Console.WriteLine($"[DeterministicCachingWeatherProvider] NULL for key='{key}'");
        }

        var presentAfter = _cache.TryGetValue(key, out _);
        Console.WriteLine($"[DeterministicCachingWeatherProvider] TryGetValue after: present={presentAfter} key='{key}'");
        _http.HttpContext?.Response.Headers.Append("X-Cache-After", presentAfter ? "true" : "false");

        return fresh;
    }
}

