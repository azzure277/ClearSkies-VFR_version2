using ClearSkies.Domain.Options;
using Microsoft.Extensions.Caching.Memory;
using ClearSkies.Infrastructure;

using System.Linq;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Weather;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class TestWebAppFactory : WebApplicationFactory<ClearSkies.Api.Program>
{
    public delegate IWeatherProvider ProviderFactory();

    private readonly ProviderFactory _providerFactory;
    private readonly IAirportCatalog _airports;
    private readonly ClearSkies.Domain.Aviation.IRunwayCatalog _runways;
    private readonly int _cacheMinutes;

    public TestWebAppFactory(ProviderFactory providerFactory,
                             IAirportCatalog? airports = null,
                             ClearSkies.Domain.Aviation.IRunwayCatalog? runways = null,
                             int cacheMinutes = 1)
    {
        _providerFactory = providerFactory;
        _airports = airports ?? new InMemoryAirportsStub();
        _runways = runways ?? new InMemoryRunwaysStub();
        _cacheMinutes = cacheMinutes;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(svc =>
        {
            // Remove any existing IWeatherProvider/IAirportCatalog/IRunwayCatalog registrations
            var toRemove = svc.Where(d =>
                d.ServiceType == typeof(IWeatherProvider) ||
                d.ServiceType == typeof(IAirportCatalog) ||
                d.ServiceType == typeof(ClearSkies.Domain.Aviation.IRunwayCatalog)).ToList();
            foreach (var d in toRemove) svc.Remove(d);

            // Options favorable for tests
            svc.PostConfigure<WeatherOptions>(o =>
            {
                o.CacheMinutes = _cacheMinutes;  // allow per-test control
                o.StaleAfterMinutes = 15;
                o.ServeStaleUpToMinutes = 120;   // allow stale serving
            });

            svc.AddMemoryCache();
            svc.AddScoped<ClearSkies.Domain.Diagnostics.ICacheStamp, ClearSkies.Domain.Diagnostics.CacheStamp>();

            // Register the inner toggle provider as a unique type
            svc.AddScoped<ToggleWeatherProvider>(sp => (ToggleWeatherProvider)_providerFactory());
            svc.AddScoped<IWeatherProvider>(sp =>
                new CachingWeatherProvider(
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ToggleWeatherProvider>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>(),
                    sp.GetRequiredService<ClearSkies.Domain.Diagnostics.ICacheStamp>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachingWeatherProvider>>()
                ));

            svc.AddSingleton(_airports);
            svc.AddSingleton(_runways);
        });
    }
}

// ===== Simple stubs =====
public sealed class InMemoryAirportsStub : IAirportCatalog
{
    public int? GetElevationFt(string icao) => string.Equals(icao, "KSFO", System.StringComparison.OrdinalIgnoreCase) ? 13 : (int?)null;
}

public sealed class InMemoryRunwaysStub : ClearSkies.Domain.Aviation.IRunwayCatalog
{
    public bool TryGetMagneticHeading(string icao, string runwayDesignator, out int magneticHeadingDeg)
    {
        if (icao.Equals("KSFO", System.StringComparison.OrdinalIgnoreCase) &&
            runwayDesignator.Equals("28L", System.StringComparison.OrdinalIgnoreCase))
        {
            magneticHeadingDeg = 280;
            return true;
        }
        magneticHeadingDeg = default;
        return false;
    }

    public AirportRunways? GetAirportRunways(string icao)
    {
        if (icao.Equals("KSFO", System.StringComparison.OrdinalIgnoreCase))
        {
            return new AirportRunways(
                icao,
                new List<RunwayInfo> {
                    new RunwayInfo("28L", 28, RunwaySide.Left, 280)
                }
            );
        }
        return null;
    }
}
