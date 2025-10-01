using System.Diagnostics;

namespace ClearSkies.Api.Observability
{
    /// <summary>
    /// Metrics collection for cache performance and API operations
    /// </summary>
    public interface IMetricsCollector
    {
        void RecordCacheHit(string cacheType, string key);
        void RecordCacheMiss(string cacheType, string key);
        void RecordCacheStale(string cacheType, string key);
        void RecordApiRequest(string endpoint, string method, int statusCode, double durationMs);
        void RecordWeatherProviderCall(string provider, string icao, bool success, double durationMs);
        void IncrementCounter(string name, IDictionary<string, object>? tags = null);
        void RecordValue(string name, double value, IDictionary<string, object>? tags = null);
    }

    public sealed class MetricsCollector : IMetricsCollector
    {
        private readonly ILogger<MetricsCollector> _logger;
        
        // Activity sources for distributed tracing
        public static readonly ActivitySource ActivitySource = new("ClearSkies.Api");

        public MetricsCollector(ILogger<MetricsCollector> logger)
        {
            _logger = logger;
        }

        public void RecordCacheHit(string cacheType, string key)
        {
            _logger.LogInformation("Cache HIT: {CacheType} key={Key}", cacheType, key);
            IncrementCounter("cache_operations_total", new Dictionary<string, object>
            {
                ["cache_type"] = cacheType,
                ["result"] = "hit"
            });
        }

        public void RecordCacheMiss(string cacheType, string key)
        {
            _logger.LogInformation("Cache MISS: {CacheType} key={Key}", cacheType, key);
            IncrementCounter("cache_operations_total", new Dictionary<string, object>
            {
                ["cache_type"] = cacheType,
                ["result"] = "miss"
            });
        }

        public void RecordCacheStale(string cacheType, string key)
        {
            _logger.LogWarning("Cache STALE: {CacheType} key={Key}", cacheType, key);
            IncrementCounter("cache_operations_total", new Dictionary<string, object>
            {
                ["cache_type"] = cacheType,
                ["result"] = "stale"
            });
        }

        public void RecordApiRequest(string endpoint, string method, int statusCode, double durationMs)
        {
            var level = statusCode >= 500 ? LogLevel.Error : 
                       statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
            
            _logger.Log(level, "API Request: {Method} {Endpoint} -> {StatusCode} ({DurationMs}ms)", 
                method, endpoint, statusCode, durationMs);
            
            IncrementCounter("api_requests_total", new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["status_code"] = statusCode.ToString()
            });
            
            RecordValue("api_request_duration_ms", durationMs, new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["method"] = method
            });
        }

        public void RecordWeatherProviderCall(string provider, string icao, bool success, double durationMs)
        {
            var level = success ? LogLevel.Information : LogLevel.Error;
            _logger.Log(level, "Weather Provider: {Provider} ICAO={Icao} Success={Success} ({DurationMs}ms)", 
                provider, icao, success, durationMs);
            
            IncrementCounter("weather_provider_calls_total", new Dictionary<string, object>
            {
                ["provider"] = provider,
                ["success"] = success.ToString()
            });
            
            RecordValue("weather_provider_duration_ms", durationMs, new Dictionary<string, object>
            {
                ["provider"] = provider
            });
        }

        public void IncrementCounter(string name, IDictionary<string, object>? tags = null)
        {
            var tagsStr = tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "";
            _logger.LogDebug("METRIC Counter: {Name} [{Tags}]", name, tagsStr);
        }

        public void RecordValue(string name, double value, IDictionary<string, object>? tags = null)
        {
            var tagsStr = tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "";
            _logger.LogDebug("METRIC Value: {Name}={Value} [{Tags}]", name, value, tagsStr);
        }
    }
}