
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Xunit;
using ClearSkies.Domain; // For Metar
using ClearSkies.Domain.Aviation; // For IWeatherProvider

    // Failing provider for stale-on-error test
    public class FailingMetarSource : ClearSkies.Infrastructure.IMetarSource
    {
        public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default) => Task.FromResult<Metar?>(null);
    }

namespace ClearSkies.Tests
{
    public class CachingWeatherProviderTests
    {
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DefaultHttpContext _httpContext;

        public CachingWeatherProviderTests()
        {
            var services = new ServiceCollection();
            TestServiceSetup.ConfigureTestServices(services);
            var provider = services.BuildServiceProvider();
            _cache = provider.GetRequiredService<IMemoryCache>();
            _httpContext = new DefaultHttpContext();
            _httpContextAccessor = new HttpContextAccessor { HttpContext = _httpContext };
        }

        [Fact]
        public async Task MissThenHit_SetsCacheHeaderCorrectly()
        {
            // Arrange
            var inner = new FakeMetarSource();
            var provider = new CachingWeatherProvider(
                _cache,
                inner,
                Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 10 }),
                _httpContextAccessor,
                () => DateTime.UtcNow // Use real clock for this test
            );
            var icao = "KSFO";

            // Act - first call (MISS)
            var result1 = await provider.GetLatestAsync(icao, CancellationToken.None);
            var header1 = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Act - second call (HIT)
            var result2 = await provider.GetLatestAsync(icao, CancellationToken.None);
            var header2 = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Assert
            Assert.Equal("false", header1);
            Assert.Equal("true", header2);
        }

        // Add your stale-on-error test here as needed, depending on your error handling logic.
        [Fact]
        public async Task StaleOnError_ThresholdBehavior()
        {
            // Arrange
            var icao = "KDEN";
            var fixedNow = System.DateTime.UtcNow;
            var metar = new Metar(icao, fixedNow.AddMinutes(-9), 0, 0, 0, 0, 0, 0, 0, 0); // Age 9 min
            _cache.Set($"metar:{icao}", metar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });

            // Simulate upstream failure (returns null)
            var inner = new FailingMetarSource();
            var provider = new CachingWeatherProvider(
                _cache,
                inner,
                Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 10 }),
                _httpContextAccessor,
                () => fixedNow // Use fixed clock for deterministic test
            );

            // Act - should serve stale (below threshold)
            var result = await provider.GetLatestAsync(icao, CancellationToken.None);
            // Assert
            Assert.NotNull(result); // Stale served

            // Now simulate above threshold (age 11 min, threshold 10)
            var oldMetar = new Metar(icao, fixedNow.AddMinutes(-11), 0, 0, 0, 0, 0, 0, 0, 0); // Age 11 min
            _cache.Set($"metar:{icao}", oldMetar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });
            // Act - should NOT serve stale (above threshold)
            var result2 = await provider.GetLatestAsync(icao, CancellationToken.None);
            // Assert
            Assert.Null(result2); // No stale served
        }

        [Fact]
        public async Task StaleOnError_SetsWarningHeader()
        {
            // Arrange
            var icao = "KJFK";
            var fixedNow = System.DateTime.UtcNow;
            // Set cache entry age to 5 min, CacheMinutes to 2, ServeStaleUpToMinutes to 10
            var metar = new Metar(icao, fixedNow.AddMinutes(-5), 0, 0, 0, 0, 0, 0, 0, 0); // Age 5 min
            _cache.Set($"metar:{icao}", metar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });

            var inner = new FailingMetarSource();
            var provider = new CachingWeatherProvider(
                _cache,
                inner,
                Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 2, ServeStaleUpToMinutes = 10 }),
                _httpContextAccessor,
                () => fixedNow
            );

            // Act
            var result = await provider.GetLatestAsync(icao, CancellationToken.None);
            // Ensure headers are set after call
            var headers = _httpContext.Response.Headers;
            var warningHeader = headers.ContainsKey("Warning") ? headers["Warning"].ToString() : string.Empty;
            var cacheHeader = headers.ContainsKey("X-Cache-Present") ? headers["X-Cache-Present"].ToString() : string.Empty;

            // Debug output: print all headers
            Console.WriteLine("Response headers after call:");
            foreach (var kvp in headers)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }

            // Assert
            Assert.NotNull(result); // Stale served
            Assert.False(string.IsNullOrEmpty(warningHeader)); // Warning header should be set
            Assert.Contains("110", warningHeader); // Warning header present
            Assert.Equal("true", cacheHeader); // Cache header present
        }
    }

    // Minimal fake provider for testing
    public class FakeMetarSource : ClearSkies.Infrastructure.IMetarSource
    {
        public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
        {
            return Task.FromResult<Metar?>(new Metar(icao, System.DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
    // Failing provider for stale-on-error test is now only defined in CachingWeatherProviderTestsTest.cs
}
