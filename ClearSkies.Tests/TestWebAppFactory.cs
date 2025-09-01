using Microsoft.AspNetCore.Http;
using ClearSkies.Tests;
using ClearSkies.Infrastructure; // For IMetarSource

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using ClearSkies.Domain;
using ClearSkies.Api;

// Test double for deterministic METARs
public sealed class TestWeatherProvider : IMetarSource
{
    public Metar? NextMetar { get; set; }
    public bool FailNext { get; set; }

    public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new HttpRequestException("Simulated upstream failure");
        }
        // Always return the seeded METAR unless explicitly set to null
        return Task.FromResult(NextMetar);
    }
}

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public TestWeatherProvider TestProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 1) Use ONE shared MemoryCache across requests
            services.RemoveAll<IMemoryCache>();
            services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions { TrackStatistics = true }));
            services.AddHttpContextAccessor();

            // 2) Replace the *inner* provider with the test double
            services.RemoveAll<IMetarSource>();
            services.AddSingleton<TestWeatherProvider>(TestProvider);

            // 3) Re-introduce the *caching decorator* as the IMetarSource
            services.AddSingleton<IMetarSource>(sp =>
                new CachingWeatherProvider(
                    cache: sp.GetRequiredService<IMemoryCache>(),
                    inner: sp.GetRequiredService<TestWeatherProvider>(),
                    opt: Microsoft.Extensions.Options.Options.Create(new WeatherOptions { CacheMinutes = 10 }),
                    httpContextAccessor: sp.GetRequiredService<IHttpContextAccessor>(),
                    etagService: new ClearSkies.Api.Http.EtagService()
                ));
        });
    }
}
