using System;
using Microsoft.Extensions.Caching.Memory;
using ClearSkies.Domain;

namespace ClearSkies.Infrastructure
{
    public class MemoryMetarCache : IMetarCache
    {
        private readonly IMemoryCache _cache;
        public MemoryMetarCache(IMemoryCache cache)
        {
            _cache = cache;
        }
        public bool TryGet(string icao, out Metar? metar, DateTime now)
        {
            return _cache.TryGetValue(GetKey(icao), out metar);
        }
        public bool TryGetStale(string icao, out Metar? metar)
        {
            // IMemoryCache does not expose expiration, so just try get
            return _cache.TryGetValue(GetKey(icao), out metar);
        }
        public void Set(string icao, Metar metar, DateTime now, TimeSpan ttl)
        {
            _cache.Set(GetKey(icao), metar, ttl);
        }
        public void Invalidate(string icao)
        {
            _cache.Remove(GetKey(icao));
        }
        private static string GetKey(string icao) => $"metar:{icao.ToUpperInvariant()}";
    }
}
