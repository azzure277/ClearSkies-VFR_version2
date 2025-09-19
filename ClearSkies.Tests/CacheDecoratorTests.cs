using ClearSkies.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain.Diagnostics;
using ClearSkies.Domain.Options;
using ClearSkies.Api;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class FakeCacheStamp : ICacheStamp
{
    public string? Result { get; set; }
}

public class FakeLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    IDisposable Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class CacheDecoratorTests
{
    [Fact]
    public async Task Miss_then_Hit_then_Fallback_when_inner_fails()
    {
        // Arrange a 5-min-old METAR
        var primed = new Metar(
            "KSFO",
            DateTime.UtcNow.AddMinutes(-5), // Age 5 min
            180,
            10,
            null,
            10,
            5000,
            20,
            10,
            29.92m
        );

        var inner = Substitute.For<IWeatherProvider>();
        inner.GetMetarAsync("KSFO", Arg.Any<CancellationToken>())
             .Returns(ci => primed, ci => primed);

        // Shared cache survives across SUT instances
        var sharedCache = new MemoryCache(new MemoryCacheOptions());

        var optsFresh = Options.Create(new WeatherOptions {
            CacheMinutes = 30, StaleAfterMinutes = 15, ServeStaleUpToMinutes = 120
        });

        var stamp = new FakeCacheStamp();
        var logger = new FakeLogger<CachingWeatherProvider>();
        var sut = new CachingWeatherProvider(sharedCache, inner, optsFresh, stamp, logger);

        // 1) MISS -> store
        _ = await sut.GetMetarAsync("KSFO");
        stamp.Result.Should().Be("MISS");

        // 2) HIT (still fresh)
        _ = await sut.GetMetarAsync("KSFO");
        stamp.Result.Should().Be("HIT");

        // Now force refresh path: failing inner + tiny cache window, but KEEP the same cache
        var failingInner = new ThrowingMetarSource();
        var optsForceRefresh = Options.Create(new WeatherOptions {
            CacheMinutes = 0,            // treat as expired so refresh is attempted
            StaleAfterMinutes = 15,
            ServeStaleUpToMinutes = 120  // allow fallback
        });

        // 1. First call: cache is empty, provider throws, fallback not possible yet
        stamp = new FakeCacheStamp();
        sut = new CachingWeatherProvider(sharedCache, failingInner, optsForceRefresh, stamp, logger);
        try
        {
            await sut.GetMetarAsync("KSFO");
        }
        catch { /* expected */ }

        // 2. Insert stale entry after failure (simulate a stale but valid fallback)
        var staleObserved = DateTime.UtcNow.AddMinutes(-5); // 5 min old, older than CacheMinutes=0, but within ServeStaleUpToMinutes=120
        var staleMetar = new Metar(
            "KSFO",
            staleObserved,
            180,
            10,
            null,
            10,
            5000,
            20,
            10,
            29.92m
        );
        sharedCache.Remove($"metar:KSFO"); // Ensure cache is clear
        sharedCache.Set($"metar:KSFO", staleMetar, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        // 3. Second call: provider throws, fallback should be served
        var m3 = await sut.GetMetarAsync("KSFO");
        m3.Should().NotBeNull();
        stamp.Result.Should().Be("FALLBACK");
    }

    // Throws on every call
    public class ThrowingMetarSource : IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default) => throw new Exception("fail");
    }
}
