
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
            var inner = new FakeWeatherProvider();
            var provider = new CachingWeatherProvider(_cache, inner, Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 10 }), _httpContextAccessor);
            var icao = "KSFO";

            // Act - first call (MISS)
            var result1 = await provider.GetMetarAsync(icao, CancellationToken.None);
            var header1 = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Act - second call (HIT)
            var result2 = await provider.GetMetarAsync(icao, CancellationToken.None);
            var header2 = _httpContext.Response.Headers["X-Cache-Present"].ToString();

            // Assert
            Assert.Equal("false", header1);
            Assert.Equal("true", header2);
        }

        // Add your stale-on-error test here as needed, depending on your error handling logic.
    }

    // Minimal fake provider for testing
    public class FakeWeatherProvider : IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            return Task.FromResult<Metar?>(new Metar(icao, System.DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}
