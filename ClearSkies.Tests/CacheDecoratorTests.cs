using ClearSkies.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain.Diagnostics;
using ClearSkies.Domain.Options;
using ClearSkies.Infrastructure.Weather;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class CacheDecoratorTests
{
    [Fact]
    public async Task Miss_then_Hit_then_Fallback_when_inner_fails()
    {
        // Arrange a 5-min-old METAR
        var primed = new Metar(
            "KSFO",
            DateTime.UtcNow.AddMinutes(-5),
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

        var stamp = new CacheStamp();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider>>();
        var sut = new CachingWeatherProvider(sharedCache, inner, optsFresh, stamp, logger);

        // 1) MISS -> store
        _ = await sut.GetMetarAsync("KSFO");
        stamp.Result.Should().Be("MISS");

        // 2) HIT (still fresh)
        _ = await sut.GetMetarAsync("KSFO");
        stamp.Result.Should().Be("HIT");

        // Now force refresh path: failing inner + tiny cache window, but KEEP the same cache
        var failingInner = Substitute.For<IWeatherProvider>();
        failingInner.GetMetarAsync("KSFO", Arg.Any<CancellationToken>())
                    .Returns<Task<Metar?>>(_ => throw new HttpRequestException("fail"));

        var optsForceRefresh = Options.Create(new WeatherOptions {
            CacheMinutes = 0,            // treat as expired so refresh is attempted
            StaleAfterMinutes = 15,
            ServeStaleUpToMinutes = 120  // allow fallback
        });

        stamp = new CacheStamp();
        sut = new CachingWeatherProvider(sharedCache, failingInner, optsForceRefresh, stamp, logger);

        // 3) FALLBACK (serve cached because refresh failed)
        var m3 = await sut.GetMetarAsync("KSFO");
        m3.Should().NotBeNull();
        stamp.Result.Should().Be("FALLBACK");
    }
}
