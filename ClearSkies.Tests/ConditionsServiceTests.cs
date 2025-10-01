using ClearSkies.Infrastructure;
using ClearSkies.Api.Services;
using ClearSkies.Domain.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
using ClearSkies.Domain.Diagnostics;
using NSubstitute;
using Microsoft.Extensions.Options;
using Xunit;

public class ConditionsServiceTests
{
    [Fact]
    public async Task Marks_Stale_When_Age_Exceeds_Threshold()
    {
        // arrange
        var metar = new Metar(
            "KSFO",
            DateTime.UtcNow.AddMinutes(-20),
            270, 10, null, 10, 500, 20, 10, 29.92m
        );
        var provider = Substitute.For<IWeatherProvider>();
    provider.GetMetarAsync("KSFO", Arg.Any<CancellationToken>()).Returns(Task.FromResult<Metar?>(metar));

        var opts = Options.Create(new WeatherOptions { StaleAfterMinutes = 15 });
        var stamp = Substitute.For<ICacheStamp>();
        var catalog = Substitute.For<IAirportCatalog>();
        var runways = Substitute.For<ClearSkies.Domain.Aviation.IRunwayCatalog>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ConditionsService>>();
        var cache = Substitute.For<IConditionsCache>();
        
        // Set up cache to call the factory (bypass cache)
        cache.GetCachedConditionsAsync(Arg.Any<string>(), Arg.Any<Func<Task<AirportConditionsDto?>>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<bool>())
             .Returns(callInfo => callInfo.Arg<Func<Task<AirportConditionsDto?>>>()());
        
        var svc = new ConditionsService(provider, catalog, runways, opts, logger, stamp, cache);

        // act
        var dto = await svc.GetConditionsAsync("KSFO", 280, CancellationToken.None);

        // assert
        Assert.True(dto!.IsStale);
        Assert.True(dto.AgeMinutes >= 20);
    }
}
