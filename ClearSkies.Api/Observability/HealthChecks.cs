using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using ClearSkies.Domain;
using System.Diagnostics;

namespace ClearSkies.Api.Observability
{
    /// <summary>
    /// Health check for weather provider connectivity
    /// </summary>
    public class WeatherProviderHealthCheck : IHealthCheck
    {
        private readonly IWeatherProvider _weatherProvider;
        private readonly ILogger<WeatherProviderHealthCheck> _logger;

        public WeatherProviderHealthCheck(IWeatherProvider weatherProvider, ILogger<WeatherProviderHealthCheck> logger)
        {
            _weatherProvider = weatherProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Use a well-known airport for health check (KORD - Chicago O'Hare)
                var metar = await _weatherProvider.GetMetarAsync("KORD", cancellationToken);
                
                stopwatch.Stop();
                var duration = stopwatch.ElapsedMilliseconds;

                if (metar != null)
                {
                    _logger.LogInformation("Weather provider health check passed: KORD data retrieved in {DurationMs}ms", duration);
                    
                    var data = new Dictionary<string, object>
                    {
                        ["icao"] = metar.Icao,
                        ["observed"] = metar.Observed.ToString("O"),
                        ["cache_result"] = metar.CacheResult ?? "unknown",
                        ["duration_ms"] = duration
                    };

                    return HealthCheckResult.Healthy($"Weather provider healthy (KORD retrieved in {duration}ms)", data);
                }
                else
                {
                    _logger.LogWarning("Weather provider health check failed: No data returned for KORD in {DurationMs}ms", duration);
                    return HealthCheckResult.Degraded($"Weather provider returned no data for KORD (took {duration}ms)");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Weather provider health check canceled");
                return HealthCheckResult.Unhealthy("Weather provider health check was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weather provider health check failed with exception");
                return HealthCheckResult.Unhealthy($"Weather provider error: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Health check for memory cache status
    /// </summary>
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheHealthCheck> _logger;

        public CacheHealthCheck(IMemoryCache memoryCache, ILogger<CacheHealthCheck> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Test cache by storing and retrieving a test value
                var testKey = "__health_check_test__";
                var testValue = DateTime.UtcNow.ToString("O");
                
                _memoryCache.Set(testKey, testValue, TimeSpan.FromSeconds(1));
                var retrieved = _memoryCache.Get<string>(testKey);
                _memoryCache.Remove(testKey);

                if (retrieved == testValue)
                {
                    _logger.LogDebug("Cache health check passed: test value stored and retrieved successfully");
                    
                    var data = new Dictionary<string, object>
                    {
                        ["test_key"] = testKey,
                        ["status"] = "operational"
                    };

                    return Task.FromResult(HealthCheckResult.Healthy("Memory cache operational", data));
                }
                else
                {
                    _logger.LogWarning("Cache health check failed: test value mismatch");
                    return Task.FromResult(HealthCheckResult.Degraded("Memory cache test value mismatch"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed with exception");
                return Task.FromResult(HealthCheckResult.Unhealthy($"Memory cache error: {ex.Message}", ex));
            }
        }
    }
}