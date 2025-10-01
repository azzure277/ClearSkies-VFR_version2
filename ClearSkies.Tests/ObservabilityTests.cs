using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using ClearSkies.Api.Observability;
using ClearSkies.Domain;

namespace ClearSkies.Tests;

public class ObservabilityTests
{
    [Fact]
    public void MetricsCollector_RecordCacheHit_LogsCorrectly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollector>>();
        var metrics = new MetricsCollector(logger);

        // Act
        metrics.RecordCacheHit("conditions", "KORD");

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cache HIT") && o.ToString()!.Contains("conditions") && o.ToString()!.Contains("KORD")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void MetricsCollector_RecordApiRequest_LogsWithCorrectLevel()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollector>>();
        var metrics = new MetricsCollector(logger);

        // Act - Normal request
        metrics.RecordApiRequest("/airports/KORD/conditions", "GET", 200, 150.5);
        
        // Act - Error request
        metrics.RecordApiRequest("/airports/KORD/conditions", "GET", 500, 250.0);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("200") && o.ToString()!.Contains("150.5")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("500") && o.ToString()!.Contains("250")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CacheHealthCheck_WithWorkingCache_ReturnsHealthy()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = Substitute.For<ILogger<CacheHealthCheck>>();
        var healthCheck = new CacheHealthCheck(memoryCache, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Memory cache operational");
        result.Data.Should().ContainKey("test_key");
        result.Data.Should().ContainKey("status");
    }

    [Fact]
    public async Task WeatherProviderHealthCheck_WithWorkingProvider_ReturnsHealthy()
    {
        // Arrange
        var weatherProvider = Substitute.For<IWeatherProvider>();
        var logger = Substitute.For<ILogger<WeatherProviderHealthCheck>>();
        
        var testMetar = new Metar("KORD", DateTime.UtcNow.AddMinutes(-5), 270, 10, null, 10, 1500, 20, 15, 29.92m);
        testMetar.CacheResult = "HIT";
        testMetar.RawMetar = "METAR KORD 151856Z 27010KT 10SM FEW015 SCT250 20/15 A2992 RMK AO2 SLP135 T02000150";
        weatherProvider.GetMetarAsync("KORD", Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<Metar?>(testMetar));

        var healthCheck = new WeatherProviderHealthCheck(weatherProvider, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Weather provider healthy");
        result.Data.Should().ContainKey("icao");
        result.Data.Should().ContainKey("observed");
        result.Data.Should().ContainKey("duration_ms");
    }

    [Fact]
    public async Task WeatherProviderHealthCheck_WithFailingProvider_ReturnsUnhealthy()
    {
        // Arrange
        var weatherProvider = Substitute.For<IWeatherProvider>();
        var logger = Substitute.For<ILogger<WeatherProviderHealthCheck>>();
        
        weatherProvider.GetMetarAsync("KORD", Arg.Any<CancellationToken>())
                      .Returns(Task.FromException<Metar?>(new Exception("Provider unavailable")));

        var healthCheck = new WeatherProviderHealthCheck(weatherProvider, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Weather provider error");
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task WeatherProviderHealthCheck_WithNoData_ReturnsDegraded()
    {
        // Arrange
        var weatherProvider = Substitute.For<IWeatherProvider>();
        var logger = Substitute.For<ILogger<WeatherProviderHealthCheck>>();
        
        weatherProvider.GetMetarAsync("KORD", Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<Metar?>(null));

        var healthCheck = new WeatherProviderHealthCheck(weatherProvider, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Weather provider returned no data");
    }
}