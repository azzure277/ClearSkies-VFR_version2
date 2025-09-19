using ClearSkies.Api;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Xunit;

using ClearSkies.Domain; // For Metar
using ClearSkies.Domain.Aviation; // For IWeatherProvider
using ClearSkies.Domain.Options;
using ClearSkies.Domain.Diagnostics;
using Microsoft.Extensions.Logging;

    // Failing provider for stale-on-error test
    public class FailingMetarSource : ClearSkies.Domain.IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default) => Task.FromResult<Metar?>(null);
    }

namespace ClearSkies.Tests
{
    public class CachingWeatherProviderTests
    {
        private IMemoryCache CreateCache() => new MemoryCache(new MemoryCacheOptions());
        private (IHttpContextAccessor, DefaultHttpContext) CreateContext()
        {
            var ctx = new DefaultHttpContext();
            return (new HttpContextAccessor { HttpContext = ctx }, ctx);
        }

        [Fact]
        public async Task MissThenHit_SetsCacheResultCorrectly()
        {
            // Arrange
            var cache = CreateCache();
            var inner = new FakeMetarSource();
            var stamp = new FakeCacheStamp();
            var provider = new CachingWeatherProvider(
                cache,
                inner,
                Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 10 }),
                stamp,
                new FakeLogger<CachingWeatherProvider>()
            );
            var icao = "KSFO";

            // Act - first call (MISS)
            var result1 = await provider.GetMetarAsync(icao, CancellationToken.None);
            var missResult = stamp.Result;

            // Act - second call (HIT)
            var result2 = await provider.GetMetarAsync(icao, CancellationToken.None);
            var hitResult = stamp.Result;

            // Assert
            Assert.Equal("MISS", missResult);
            Assert.Equal("HIT", hitResult);
        }

        [Fact]
        public async Task StaleOnError_SetsFallbackResult()
        {
            // Arrange
            var cache = CreateCache();
            var icao = "KJFK";
            var inner = new ThrowingMetarSource();
            var stamp = new FakeCacheStamp();
            var provider = new CachingWeatherProvider(
                cache,
                inner,
                Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 2, ServeStaleUpToMinutes = 10 }),
                stamp,
                new FakeLogger<CachingWeatherProvider>()
            );

            // 1. First call: cache is empty, provider throws, fallback not possible yet
            try
            {
                await provider.GetMetarAsync(icao, CancellationToken.None);
            }
            catch { /* expected */ }

            // 2. Insert stale entry after failure (simulate a stale but valid fallback)
            var staleObserved = System.DateTime.UtcNow.AddMinutes(-5); // 5 min old, older than CacheMinutes=2, but within ServeStaleUpToMinutes=10
            var staleMetar = new Metar(icao, staleObserved, 0, 0, 0, 0, 0, 0, 0, 0);
            cache.Remove($"metar:{icao.ToUpperInvariant()}"); // Ensure cache is clear
            cache.Set($"metar:{icao.ToUpperInvariant()}", staleMetar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });

            // 3. Second call: provider throws, fallback should be served
            var result2 = await provider.GetMetarAsync(icao, CancellationToken.None);
            Assert.NotNull(result2); // Stale served
            Assert.Equal("FALLBACK", stamp.Result); // Should be fallback
        }

        // Throws on every call
        public class ThrowingMetarSource : ClearSkies.Domain.IWeatherProvider
        {
            public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default) => throw new System.Exception("fail");
        }
    }

    // Minimal fake provider for testing
    public class FakeMetarSource : ClearSkies.Domain.IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            return Task.FromResult<Metar?>(new Metar(icao, System.DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }

    public class FakeCacheStamp : ICacheStamp
    {
        public string? Result { get; set; }
    }

    public class FakeLogger<T> : ILogger<T>
    {
        IDisposable ILogger.BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
    // Failing provider for stale-on-error test is now only defined in CachingWeatherProviderTestsTest.cs
}
