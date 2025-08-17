namespace ClearSkies.Domain.Options
{
    public sealed class WeatherOptions
    {
        // METAR considered "fresh" if age ≤ FreshMinutes
        public int FreshMinutes { get; set; } = 30;

        // In-memory cache TTL for METAR fetches
        public int CacheMinutes { get; set; } = 5;
    }
}
