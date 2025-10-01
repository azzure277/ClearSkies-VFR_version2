using System.Diagnostics;

namespace ClearSkies.Api.Observability
{
    /// <summary>
    /// Middleware for API request/response observability
    /// </summary>
    public class ObservabilityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMetricsCollector _metrics;
        private readonly ILogger<ObservabilityMiddleware> _logger;

        public ObservabilityMiddleware(RequestDelegate next, IMetricsCollector metrics, ILogger<ObservabilityMiddleware> logger)
        {
            _next = next;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            
            // Add correlation ID to response headers for tracing
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            
            // Add correlation ID to log scope
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method
            });

            try
            {
                _logger.LogInformation("Request started: {Method} {Path} [CorrelationId: {CorrelationId}]", 
                    context.Request.Method, context.Request.Path, correlationId);

                await _next(context);

                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalMilliseconds;

                // Record metrics
                _metrics.RecordApiRequest(
                    endpoint: GetNormalizedPath(context.Request.Path),
                    method: context.Request.Method,
                    statusCode: context.Response.StatusCode,
                    durationMs: duration
                );

                _logger.LogInformation("Request completed: {Method} {Path} -> {StatusCode} ({DurationMs}ms) [CorrelationId: {CorrelationId}]",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, duration, correlationId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalMilliseconds;

                // Record error metrics
                _metrics.RecordApiRequest(
                    endpoint: GetNormalizedPath(context.Request.Path),
                    method: context.Request.Method,
                    statusCode: 500,
                    durationMs: duration
                );

                _logger.LogError(ex, "Request failed: {Method} {Path} ({DurationMs}ms) [CorrelationId: {CorrelationId}]",
                    context.Request.Method, context.Request.Path, duration, correlationId);

                throw;
            }
        }

        private static string GetNormalizedPath(PathString path)
        {
            // Normalize paths for metrics (replace ICAO codes with placeholder)
            var pathStr = path.Value ?? "";
            
            if (pathStr.StartsWith("/airports/") && pathStr.Contains("/conditions"))
            {
                return "/airports/{icao}/conditions";
            }
            
            if (pathStr.StartsWith("/airports") && !pathStr.Contains("/conditions"))
            {
                return "/airports";
            }

            return pathStr;
        }
    }

    /// <summary>
    /// Extension methods for observability middleware
    /// </summary>
    public static class ObservabilityExtensions
    {
        public static IApplicationBuilder UseObservability(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ObservabilityMiddleware>();
        }
    }
}