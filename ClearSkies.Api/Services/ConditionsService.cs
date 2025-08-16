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
    private readonly IMetarSource _metarSource;
    private readonly IAirportCatalog _catalog;
    private readonly ILogger<ConditionsService> _logger;

    public ConditionsService(
        IMetarSource metarSource,
        IAirportCatalog catalog,
        ILogger<ConditionsService> logger)
    {
        _metarSource = metarSource;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<AirportConditionsDto?> GetConditionsAsync(string icao, int runwayHeadingDeg, CancellationToken ct)
    {
        _logger.LogInformation("Fetching METAR for {ICAO}", icao);

        var metar = await _metarSource.GetLatestAsync(icao, ct);
        if (metar is null)
        {
            _logger.LogWarning("No METAR returned for {ICAO}", icao);

#if DEBUG
            const bool USE_DEV_STUB = true;
            if (!USE_DEV_STUB)
                return null;

            var now = DateTime.UtcNow;
            var stub = new Metar(icao.ToUpperInvariant(), now, 190m, 12m, 18m, 10m, 4500, 20m, 12m, 30.02m);
            var stubCat = AviationCalculations.ComputeCategory(stub.CeilingFtAgl, stub.VisibilitySm);
            var (stubHead, stubCross) = AviationCalculations.WindComponents(runwayHeadingDeg, stub.WindDirDeg, stub.WindKt);
            var stubElev = _catalog.GetElevationFt(stub.Icao) ?? 0;
            var stubDa = AviationCalculations.DensityAltitudeFt(stubElev, stub.TemperatureC, stub.AltimeterInHg);

            // Stub branch
            var stubAgeMinutes = 0;
            return new AirportConditionsDto(
                stub.Icao,
                (int)stubCat, // Cast FlightCategory to int
                stub.Observed,
                stub.WindDirDeg,
                stub.WindKt,
                stub.GustKt,
                stub.VisibilitySm,
                stub.CeilingFtAgl,
                stub.TemperatureC,
                stub.DewpointC,
                stub.AltimeterInHg,
                stubHead,
                stubCross,
                stubDa,
                false, // IsStale
                stubAgeMinutes
            );
#else
            // In release, don't use stub, just return null
            return null;
#endif
        }

        var category = AviationCalculations.ComputeCategory(metar.CeilingFtAgl, metar.VisibilitySm);
        var (head, cross) = AviationCalculations.WindComponents(runwayHeadingDeg, metar.WindDirDeg, metar.WindKt);

        // ✅ Use real field elevation from the catalog
        var fieldElevationFt = _catalog.GetElevationFt(metar.Icao) ?? 0;
        var da = AviationCalculations.DensityAltitudeFt(fieldElevationFt, metar.TemperatureC, metar.AltimeterInHg);

        // Compute staleness (older than 1 hour)
        var ageMinutes = (int)(DateTime.UtcNow - metar.Observed).TotalMinutes;
        var isStale = ageMinutes > 60;

        _logger.LogInformation("Returning conditions for {ICAO} (DA {DA} ft, Head {Head} kt, Cross {Cross} kt)",
            icao, da, head, cross);

        // Real METAR branch
        return new AirportConditionsDto(
            Icao: metar.Icao,
            Category: (int)category, // Cast FlightCategory to int
            ObservedUtc: metar.Observed,
            WindDirDeg: metar.WindDirDeg,
            WindKt: metar.WindKt,
            GustKt: metar.GustKt,
            VisibilitySm: metar.VisibilitySm,
            CeilingFtAgl: metar.CeilingFtAgl,
            TemperatureC: metar.TemperatureC,
            DewpointC: metar.DewpointC,
            AltimeterInHg: metar.AltimeterInHg,
            HeadwindKt: head,
            CrosswindKt: cross,
            DensityAltitudeFt: da,
            IsStale: isStale,
            AgeMinutes: ageMinutes
        );
    }
}
