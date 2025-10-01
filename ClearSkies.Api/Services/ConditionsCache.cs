using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;

namespace ClearSkies.Api.Services
{
    public interface IConditionsCache
    {
        Task<AirportConditionsDto?> GetCachedConditionsAsync(string cacheKey, Func<Task<AirportConditionsDto?>> factory, TimeSpan freshTtl, TimeSpan staleTtl, bool forceRefresh = false);
        void InvalidateConditions(string icao, string? runway = null);
    }

    public sealed class ConditionsCache : IConditionsCache
    {
        private readonly IMemoryCache _cache;
        private readonly Func<DateTime> _clock;
        private readonly ILogger<ConditionsCache> _logger;

        public ConditionsCache(IMemoryCache cache, ILogger<ConditionsCache> logger, Func<DateTime>? clock = null)
        {
            _cache = cache;
            _clock = clock ?? (() => DateTime.UtcNow);
            _logger = logger;
        }

        public async Task<AirportConditionsDto?> GetCachedConditionsAsync(
            string cacheKey, 
            Func<Task<AirportConditionsDto?>> factory, 
            TimeSpan freshTtl, 
            TimeSpan staleTtl,
            bool forceRefresh = false)
        {
            var now = _clock();

            // Force refresh bypasses cache completely
            if (forceRefresh)
            {
                _cache.Remove(cacheKey);
                var freshResult = await factory();
                if (freshResult != null)
                {
                    var entry = new CacheEntry { Data = freshResult, CachedAt = now };
                    _cache.Set(cacheKey, entry, staleTtl);
                    freshResult.CacheResult = "MISS";
                }
                return freshResult;
            }

            // Check for cached entry
            if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached?.Data != null)
            {
                var age = now - cached.CachedAt;
                
                // Fresh data - return immediately
                if (age <= freshTtl)
                {
                    _logger.LogDebug("Cache HIT: {CacheKey} (age: {AgeSeconds}s, fresh within {FreshTtlSeconds}s)", 
                        cacheKey, age.TotalSeconds, freshTtl.TotalSeconds);
                    cached.Data.CacheResult = "HIT";
                    return cached.Data;
                }

                // Stale but acceptable data
                if (age <= staleTtl)
                {
                    _logger.LogInformation("Cache STALE: {CacheKey} (age: {AgeSeconds}s, refreshing in background)", 
                        cacheKey, age.TotalSeconds);
                    
                    // Try to refresh in background, but return stale data immediately
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogDebug("Background refresh started for {CacheKey}", cacheKey);
                            var refreshed = await factory();
                            if (refreshed != null)
                            {
                                var entry = new CacheEntry { Data = refreshed, CachedAt = _clock() };
                                _cache.Set(cacheKey, entry, staleTtl);
                                _logger.LogDebug("Background refresh completed for {CacheKey}", cacheKey);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Background refresh failed for {CacheKey}", cacheKey);
                        }
                    });

                    cached.Data.CacheResult = "STALE";
                    return cached.Data;
                }

                // Too old - remove from cache
                _logger.LogInformation("Cache EXPIRED: {CacheKey} (age: {AgeSeconds}s, max: {StaleTtlSeconds}s)", 
                    cacheKey, age.TotalSeconds, staleTtl.TotalSeconds);
                _cache.Remove(cacheKey);
            }

            // Cache miss or expired - fetch fresh data
            _logger.LogDebug("Cache MISS: {CacheKey} - fetching fresh data", cacheKey);
            try
            {
                var result = await factory();
                if (result != null)
                {
                    var entry = new CacheEntry { Data = result, CachedAt = now };
                    _cache.Set(cacheKey, entry, staleTtl);
                    result.CacheResult = "MISS";
                }
                return result;
            }
            catch
            {
                // If factory fails, try to serve very stale data as fallback
                if (_cache.TryGetValue(cacheKey + ":fallback", out CacheEntry? fallback) && fallback?.Data != null)
                {
                    fallback.Data.CacheResult = "FALLBACK";
                    fallback.Data.IsStale = true;
                    return fallback.Data;
                }
                throw;
            }
        }

        public void InvalidateConditions(string icao, string? runway = null)
        {
            var key = GetCacheKey(icao, runway);
            _cache.Remove(key);
        }

        private static string GetCacheKey(string icao, string? runway = null)
        {
            var normalizedIcao = icao.ToUpperInvariant();
            return string.IsNullOrWhiteSpace(runway) 
                ? $"conditions:{normalizedIcao}" 
                : $"conditions:{normalizedIcao}:{runway.ToUpperInvariant()}";
        }

        private class CacheEntry
        {
            public AirportConditionsDto Data { get; set; } = null!;
            public DateTime CachedAt { get; set; }
        }
    }
}