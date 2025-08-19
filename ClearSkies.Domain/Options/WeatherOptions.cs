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

        // Minutes after which data is critically stale (big banner). null = off
        public int? CriticallyStaleAfterMinutes { get; set; } = 60;
    }
}
