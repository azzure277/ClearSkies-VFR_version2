public sealed class WeatherOptions
{
    public int FreshMinutes { get; set; } = 30;
    public int CacheMinutes { get; set; } = 5;
    /// <summary>
    /// Max age (minutes) to serve stale data if upstream fails. Null disables fallback.
    /// </summary>
    public int? ServeStaleUpToMinutes { get; set; } = 5;
}
