namespace ClearSkies.Domain.Options
{
    public sealed class WeatherOptions
    {
        // METAR considered "fresh" if age ≤ FreshMinutes
        public int FreshMinutes { get; set; } = 30;

        // In-memory cache TTL for METAR fetches
        public int CacheMinutes { get; set; } = 5;
        // Minutes after which data is stale (warn UI, still return)
        public int StaleAfterMinutes { get; set; } = 15;

    /// <summary>
    /// Max age (minutes) we are willing to serve from cache when upstream fails.
    /// Set null to disable serving fallback.
    /// </summary>
    public int? ServeStaleUpToMinutes { get; set; } = 120;

    // Minutes after which data is critically stale (big banner). null = off
    public int? CriticallyStaleAfterMinutes { get; set; } = 60;
    }
}
