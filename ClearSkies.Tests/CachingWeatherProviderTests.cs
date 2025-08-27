
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Xunit;
using ClearSkies.Domain; // For Metar
using ClearSkies.Domain.Aviation; // For IWeatherProvider

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
            _httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            _httpContext = new DefaultHttpContext();
            _httpContextAccessor.HttpContext = _httpContext;
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
            var header = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Assert
            Assert.NotNull(result); // Stale served
            Assert.Equal("true", header); // Still a cache hit

            // Now simulate above threshold (age 11 min, threshold 10)
            var oldMetar = new Metar(icao, fixedNow.AddMinutes(-11), 0, 0, 0, 0, 0, 0, 0, 0); // Age 11 min
            _cache.Set($"metar:{icao}", oldMetar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });
            _httpContext.Response.Headers.Remove("X-Cache-Present"); // Clear header

            // Act - should NOT serve stale (above threshold)
            var result2 = await provider.GetLatestAsync(icao, CancellationToken.None);
            var header2 = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Assert
            Assert.Null(result2); // No stale served
            Assert.Equal("false", header2); // Treated as miss
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

    // Failing provider for stale-on-error test
    public class FailingMetarSource : ClearSkies.Infrastructure.IMetarSource
    {
        public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default) => Task.FromResult<Metar?>(null);
    }
}
