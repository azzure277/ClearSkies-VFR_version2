
using Microsoft.Extensions.Options;
using ClearSkies.Domain.Options;
using ClearSkies.Domain;
using ClearSkies.Infrastructure;
using Microsoft.Extensions.Logging;


namespace ClearSkies.Api.Services;

public interface IConditionsService
{
    Task<AirportConditionsDto?> GetConditionsAsync(string icao, int runwayHeadingDeg, CancellationToken ct);
}

public sealed class ConditionsService : IConditionsService
{
    private readonly IWeatherProvider _weatherProvider;
    private readonly IAirportCatalog _catalog;
    private readonly ClearSkies.Domain.Aviation.IRunwayCatalog _runways;
    private readonly ILogger<ConditionsService> _logger;
    private readonly WeatherOptions _options;
    private readonly ClearSkies.Domain.Diagnostics.ICacheStamp _stamp;

    public ConditionsService(
        IWeatherProvider weatherProvider,
        IAirportCatalog catalog,
        ClearSkies.Domain.Aviation.IRunwayCatalog runways,
        IOptions<WeatherOptions> options,
        ILogger<ConditionsService> logger,
        ClearSkies.Domain.Diagnostics.ICacheStamp stamp)
    {
        _weatherProvider = weatherProvider;
        _catalog = catalog;
        _runways = runways;
        _logger = logger;
        _options = options.Value;
        _stamp = stamp;
    }

    public async Task<AirportConditionsDto?> GetConditionsAsync(string icao, int runwayHeadingDeg, CancellationToken ct)
    {
        _logger.LogInformation("Fetching METAR for {ICAO}", icao);

    var metar = await _weatherProvider.GetMetarAsync(icao, ct);
        if (metar is null)
        {
            _logger.LogWarning("No METAR returned for {ICAO}", icao);
            return null;
        }

        var category = AviationCalculations.ComputeCategory(metar.CeilingFtAgl, metar.VisibilitySm);
        var (head, cross) = AviationCalculations.WindComponents(runwayHeadingDeg, metar.WindDirDeg, metar.WindKt);

        // ✅ Use real field elevation from the catalog
        var fieldElevationFt = _catalog.GetElevationFt(metar.Icao) ?? 0;
        var da = AviationCalculations.DensityAltitudeFt(fieldElevationFt, metar.TemperatureC, metar.AltimeterInHg);

    // Compute staleness using WeatherOptions thresholds

    var nowUtc = DateTime.UtcNow;
    var ageMinutes = (int)Math.Max(0, Math.Round((nowUtc - metar.Observed).TotalMinutes));
    var isStale = ageMinutes >= _options.StaleAfterMinutes;

    // If we served cached data due to upstream failure, force stale=true
    if (string.Equals(_stamp.Result, "FALLBACK", StringComparison.OrdinalIgnoreCase))
    {
        isStale = true;
    }

        _logger.LogInformation("Returning conditions for {ICAO} (DA {DA} ft, Head {Head} kt, Cross {Cross} kt)",
            icao, da, head, cross);

        // Real METAR branch
        _logger.LogInformation($"Returning real DTO, _stamp.Result={_stamp.Result}");
        var dto = new AirportConditionsDto {
            Icao = icao.ToUpperInvariant(),
            Category = (int)category,
            ObservedUtc = metar.Observed,
            WindDirDeg = metar.WindDirDeg,
            WindKt = metar.WindKt,
            GustKt = metar.GustKt,
            VisibilitySm = metar.VisibilitySm,
            CeilingFtAgl = metar.CeilingFtAgl,
            TemperatureC = metar.TemperatureC,
            DewpointC = metar.DewpointC,
            AltimeterInHg = metar.AltimeterInHg,
            HeadwindKt = head,
            CrosswindKt = cross,
            DensityAltitudeFt = da,
            IsStale = isStale,
            AgeMinutes = ageMinutes,
            CacheResult = _stamp.Result
        };
        return dto;
    }
}
