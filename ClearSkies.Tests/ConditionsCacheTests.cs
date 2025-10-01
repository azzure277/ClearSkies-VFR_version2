using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ClearSkies.Api.Services;
using ClearSkies.Domain;

namespace ClearSkies.Tests;

public class ConditionsCacheTests
{
    private readonly MemoryCache _memoryCache;
    private readonly ConditionsCache _cache;

    public ConditionsCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<ConditionsCache>>();
        
        _cache = new ConditionsCache(_memoryCache, logger);
    }

    [Fact]
    public async Task GetCachedConditionsAsync_WhenNotCached_CallsFactory()
    {
        // Arrange
        var cacheKey = "conditions:KORD";
        var freshTtl = TimeSpan.FromMinutes(2);
        var staleTtl = TimeSpan.FromMinutes(10);
        var expectedConditions = new AirportConditionsDto { Icao = "KORD" };
        
        var factoryCalled = false;
        Task<AirportConditionsDto?> Factory()
        {
            factoryCalled = true;
            return Task.FromResult<AirportConditionsDto?>(expectedConditions);
        }

        // Act
        var result = await _cache.GetCachedConditionsAsync(cacheKey, Factory, freshTtl, staleTtl, false);

        // Assert
        factoryCalled.Should().BeTrue();
        result.Should().Be(expectedConditions);
        result?.CacheResult.Should().Be("MISS");
    }

    [Fact]
    public async Task GetCachedConditionsAsync_WithForceRefresh_AlwaysCallsFactory()
    {
        // Arrange
        var cacheKey = "conditions:KORD";
        var freshTtl = TimeSpan.FromMinutes(2);
        var staleTtl = TimeSpan.FromMinutes(10);
        var cachedConditions = new AirportConditionsDto { Icao = "KORD" };
        var freshConditions = new AirportConditionsDto { Icao = "KORD_FRESH" };
        
        // Pre-populate cache
        await _cache.GetCachedConditionsAsync(cacheKey, () => Task.FromResult<AirportConditionsDto?>(cachedConditions), freshTtl, staleTtl, false);
        
        var factoryCalled = false;
        Task<AirportConditionsDto?> Factory()
        {
            factoryCalled = true;
            return Task.FromResult<AirportConditionsDto?>(freshConditions);
        }

        // Act - force refresh even though data is fresh
        var result = await _cache.GetCachedConditionsAsync(cacheKey, Factory, freshTtl, staleTtl, true);

        // Assert
        factoryCalled.Should().BeTrue();
        result.Should().Be(freshConditions);
        result?.CacheResult.Should().Be("MISS");
    }

    [Fact]
    public async Task GetCachedConditionsAsync_CacheKeyWithRunway_WorksCorrectly()
    {
        // Arrange
        var cacheKey = "conditions:KORD:hdg270";
        var freshTtl = TimeSpan.FromMinutes(2);
        var staleTtl = TimeSpan.FromMinutes(10);
        var expectedConditions = new AirportConditionsDto { Icao = "KORD", RunwayHeadingDeg = 270 };
        
        var factoryCalled = false;
        Task<AirportConditionsDto?> Factory()
        {
            factoryCalled = true;
            return Task.FromResult<AirportConditionsDto?>(expectedConditions);
        }

        // Act
        var result = await _cache.GetCachedConditionsAsync(cacheKey, Factory, freshTtl, staleTtl, false);

        // Assert
        factoryCalled.Should().BeTrue();
        result.Should().Be(expectedConditions);
        result?.CacheResult.Should().Be("MISS");
    }
}