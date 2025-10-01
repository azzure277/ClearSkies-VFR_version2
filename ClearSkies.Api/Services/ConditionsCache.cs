using Microsoft.Extensions.Caching.Memory;
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

        public ConditionsCache(IMemoryCache cache, Func<DateTime>? clock = null)
        {
            _cache = cache;
            _clock = clock ?? (() => DateTime.UtcNow);
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
                    cached.Data.CacheResult = "HIT";
                    return cached.Data;
                }

                // Stale but acceptable data
                if (age <= staleTtl)
                {
                    // Try to refresh in background, but return stale data immediately
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var refreshed = await factory();
                            if (refreshed != null)
                            {
                                var entry = new CacheEntry { Data = refreshed, CachedAt = _clock() };
                                _cache.Set(cacheKey, entry, staleTtl);
                            }
                        }
                        catch
                        {
                            // Background refresh failed, keep serving stale data
                        }
                    });

                    cached.Data.CacheResult = "HIT";
                    cached.Data.IsStale = true;
                    return cached.Data;
                }

                // Too old - remove from cache
                _cache.Remove(cacheKey);
            }

            // Cache miss or expired - fetch fresh data
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