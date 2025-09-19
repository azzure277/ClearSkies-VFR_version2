using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
// using ClearSkies.Tests; // Remove to avoid confusion
using ClearSkies.Infrastructure; // For IMetarSource

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using ClearSkies.Domain;
using ClearSkies.Api;

using System;
// Add using for TestAirportCatalog in global namespace
using ClearSkies.Tests;



namespace ClearSkies.Tests
{
    // Test double for deterministic METARs
    public sealed class TestWeatherProvider : IMetarSource, ClearSkies.Domain.IWeatherProvider
    {
    public Metar? NextMetar { get; set; }
        public bool FailNext { get; set; }

        public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
            => GetMetarAsync(icao, ct);

        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            if (FailNext)
            {
                FailNext = false;
                Console.WriteLine($"[TestWeatherProvider] Simulating upstream failure for {icao}");
                throw new HttpRequestException("Simulated upstream failure");
            }
            // Return the seeded METAR once, then null to allow cache HIT
            var metar = NextMetar;
            NextMetar = null;
            if (metar != null)
            {
                metar = new Metar(
                    metar.Icao,
                    TestWebAppFactory._clock(),
                    metar.WindDirDeg,
                    metar.WindKt,
                    metar.GustKt,
                    metar.VisibilitySm,
                    metar.CeilingFtAgl,
                    metar.TemperatureC,
                    metar.DewpointC,
                    metar.AltimeterInHg
                )
                {
                    RawMetar = metar.RawMetar
                };
                Console.WriteLine($"[TestWeatherProvider] Returning seeded METAR for {icao} at {metar.Observed:o}");
            }
            else
            {
                Console.WriteLine($"[TestWeatherProvider] Returning null for {icao} (should trigger cache)");
            }
            return Task.FromResult(metar);
        }
    }


    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
    // Use a fixed clock for deterministic cache age in tests
    internal static DateTime _fixedNow = new DateTime(2025, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    internal static Func<DateTime> _clock = () => _fixedNow;
    public TestWeatherProvider TestProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                var dict = new Dictionary<string, string?>
                {
                    ["UseStaticTestCache"] = "true",
                    ["Weather:ServeStaleUpToMinutes"] = "120"
                };
                configBuilder.AddInMemoryCollection(dict);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMemoryCache>();
                services.AddMemoryCache();
                services.AddHttpContextAccessor();

                services.RemoveAll<IMetarCache>();
                services.AddSingleton<IMetarCache>(sp => new StaticTestMetarCache(_clock));

                services.RemoveAll<IWeatherProvider>();
                services.RemoveAll<IMetarSource>();
                services.AddSingleton<TestWeatherProvider>(TestProvider);
                services.AddSingleton<IMetarSource>(sp => sp.GetRequiredService<TestWeatherProvider>());
                // Register CachingWeatherProvider as IWeatherProvider, wrapping TestWeatherProvider
                // Register CachingWeatherProvider as IWeatherProvider, wrapping TestWeatherProvider (cache bypassed)
                services.AddSingleton<ClearSkies.Infrastructure.Weather.CachingWeatherProvider>(sp =>
                    new ClearSkies.Infrastructure.Weather.CachingWeatherProvider(
                        new ClearSkies.Infrastructure.MetarSourceWeatherProviderAdapter(sp.GetRequiredService<IMetarSource>()),
                        _clock
                    ));
                services.AddSingleton<IWeatherProvider>(sp => sp.GetRequiredService<ClearSkies.Infrastructure.Weather.CachingWeatherProvider>());

                // Replace the airport catalog with a test version containing KSFO, KJFK, KLAX
                services.RemoveAll<IAirportCatalog>();
                services.AddSingleton<IAirportCatalog, TestAirportCatalog>();
            });
        }
    }

}

public class AirportConditionsDtoLike
{
    // Add members as needed
}
