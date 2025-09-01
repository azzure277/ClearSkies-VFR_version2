<<<<<<< HEAD
// ...existing code...
=======

using ClearSkies.Domain.Options;
>>>>>>> master
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace ClearSkies.Api
{
<<<<<<< HEAD
    public sealed class CachingWeatherProvider : IMetarSource
    {
        private readonly IMemoryCache _cache;
        private readonly IMetarSource _inner;
        private readonly IOptions<WeatherOptions> _opt;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly Func<DateTime> _clock;
        private readonly ClearSkies.Api.Http.IEtagService _etagService;

        public CachingWeatherProvider(
            IMemoryCache cache,
            IMetarSource inner,
            IOptions<WeatherOptions> opt,
            IHttpContextAccessor? httpContextAccessor,
            ClearSkies.Api.Http.IEtagService etagService,
            Func<DateTime>? clock = null)
        {
            _cache = cache;
            _inner = inner;
            _opt = opt;
            _httpContextAccessor = httpContextAccessor;
            _etagService = etagService;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        private string GetValidEtag(string rawMetar)
        {
            // Compute SHA256 hash of rawMetar for deterministic, RFC-compliant ETag
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawMetar ?? string.Empty));
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            var validEtag = $"W/\"{hex}\"";
            System.Diagnostics.Debug.WriteLine($"[DIAG] Provider computed SHA256 ETag: {validEtag}");
            return validEtag;
        }

        public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(icao))
                return null;
            var key = $"metar:{icao.Trim().ToUpperInvariant()}";
            var ctx = _httpContextAccessor?.HttpContext;
            System.Diagnostics.Debug.WriteLine($"[DIAG] Provider cache key: {key}");
            if (_cache is MemoryCache mcBefore)
            {
                System.Diagnostics.Debug.WriteLine($"[DIAG] Provider cache count before: {mcBefore.Count}");
            }

            // Try cache HIT (early return, set header once, always remove warning)
            if (_cache.TryGetValue(key, out Metar? cached) && cached != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TRACE] Provider: CACHE HIT for {icao}");
                if (ctx?.Response?.HasStarted == false)
                {
                    ctx.Response.Headers["X-Cache-Present"] = "true";
                    // Remove Warning if present (cache hit is always fresh)
                    if (ctx.Response.Headers.ContainsKey("Warning"))
                        ctx.Response.Headers.Remove("Warning");
                    System.Diagnostics.Debug.WriteLine($"[DIAG] Set X-Cache-Present=true on cache HIT for {icao}");
                }
                return cached;
            }

            // Cache MISS
            if (ctx?.Response?.HasStarted == false)
            {
                ctx.Response.Headers["X-Cache-Present"] = "false";
                if (ctx.Response.Headers.ContainsKey("Warning"))
                    ctx.Response.Headers.Remove("Warning");
                System.Diagnostics.Debug.WriteLine($"[TRACE] Provider: CACHE MISS for {icao}");
            }
            var fresh = await _inner.GetLatestAsync(icao, ct);
            if (ctx?.Response?.HasStarted == false)
            {
                if (ctx.Response.Headers.ContainsKey("Warning"))
                    ctx.Response.Headers.Remove("Warning");
            }
            if (fresh != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TRACE] Provider: FRESH FETCH for {icao}");
                _cache.Set(key, fresh, TimeSpan.FromMinutes(_opt.Value.CacheMinutes));
                if (ctx?.Response?.HasStarted == false)
                {
                    if (ctx.Response.Headers.ContainsKey("Warning"))
                        ctx.Response.Headers.Remove("Warning");
                }
                return fresh;
            }
            // If we reach here, upstream fetch failed, try stale-on-error

            // Upstream failed, try stale-on-error logic
        if (_cache.TryGetValue(key, out Metar? stale) && stale != null && fresh == null)
        {
            var ageMinutes = (_clock() - stale.Observed).TotalMinutes;
            var cacheMinutes = _opt.Value.CacheMinutes;
            var threshold = _opt.Value.ServeStaleUpToMinutes;
            // Only serve stale if older than cacheMinutes but within threshold
            if (threshold > 0 && ageMinutes > cacheMinutes && ageMinutes <= threshold)
            {
                System.Diagnostics.Debug.WriteLine($"[DIAG] Stale-on-error: age={ageMinutes}, cacheMinutes={cacheMinutes}, threshold={threshold}, HasStarted={ctx?.Response?.HasStarted}");
                if (ctx?.Response?.HasStarted == false)
                {
                    ctx.Response.Headers["X-Cache-Present"] = "true";
                    ctx.Response.Headers["Warning"] = "110 - Response is stale due to upstream error";
                    System.Diagnostics.Debug.WriteLine($"[DIAG] Set Warning header on stale response");
                }
                return stale;
            }
        }
            // No stale available, always remove warning header
            System.Diagnostics.Debug.WriteLine($"[TRACE] Provider: NO STALE for {icao}");
            if (ctx?.Response?.HasStarted == false)
            {
                if (ctx.Response.Headers.ContainsKey("Warning"))
                    ctx.Response.Headers.Remove("Warning");
                ctx.Response.Headers["X-Cache-Present"] = "false";
            }
=======
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
>>>>>>> master
            return null;
        }
    }
}
