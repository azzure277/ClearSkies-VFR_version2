using System;
using System.Threading;
using ClearSkies.Domain;

namespace ClearSkies.Infrastructure
{
    public interface IMetarCache
    {
    bool TryGet(string icao, out Metar? metar, DateTime now);
    /// <summary>
    /// Returns the cached value even if expired (for fallback/stale scenarios).
    /// </summary>
    bool TryGetStale(string icao, out Metar? metar);
    void Set(string icao, Metar metar, DateTime now, TimeSpan ttl);
    void Invalidate(string icao);
    }
}
