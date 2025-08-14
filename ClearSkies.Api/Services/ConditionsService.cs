using ClearSkies.Domain;
using ClearSkies.Infrastructure;

namespace ClearSkies.Api.Services;

public interface IConditionsService
{
    Task<AirportConditionsDto?> GetConditionsAsync(string icao, int runwayHeadingDeg, CancellationToken ct);
}

public sealed class ConditionsService : IConditionsService
{
    private readonly IMetarSource _metarSource;
    private readonly ILogger<ConditionsService> _logger;

    public ConditionsService(IMetarSource metarSource, ILogger<ConditionsService> logger)
    {
        _metarSource = metarSource;
        _logger = logger;
    }

    public async Task<AirportConditionsDto?> GetConditionsAsync(string icao, int runwayHeadingDeg, CancellationToken ct)
    {
        _logger.LogInformation($"Fetching METAR for ICAO: {icao}");
        var metar = await _metarSource.GetLatestAsync(icao, ct);
        if (metar is null)
        {
            // TEMP STUB (remove once AVWX is working)
            var now = DateTime.UtcNow;
            var stub = new Metar(icao.ToUpperInvariant(), now, 190m, 12m, 18m, 10m, 4500, 20m, 12m, 30.02m);
            var cat = AviationCalculations.ComputeCategory(stub.CeilingFtAgl, stub.VisibilitySm);
            var (head, cross) = AviationCalculations.WindComponents(runwayHeadingDeg, stub.WindDirDeg, stub.WindKt);
            var da = AviationCalculations.DensityAltitudeFt(fieldElevationFt: 13 /*KSFO approx*/, stub.TemperatureC, stub.AltimeterInHg);

            return new AirportConditionsDto(
                stub.Icao, cat, stub.Observed, stub.WindDirDeg, stub.WindKt, stub.GustKt,
                stub.VisibilitySm, stub.CeilingFtAgl, stub.TemperatureC, stub.DewpointC,
                stub.AltimeterInHg, head, cross, da
            );
        }

        // Compute derived values
        var category = AviationCalculations.ComputeCategory(metar.CeilingFtAgl, metar.VisibilitySm);
        var (head2, cross2) = AviationCalculations.WindComponents(runwayHeadingDeg, metar.WindDirDeg, metar.WindKt);

        // TODO: replace with a real airport catalog lookup
        var fieldElevationFt2 = 433; // KSEA approx
        var da2 = AviationCalculations.DensityAltitudeFt(fieldElevationFt2, metar.TemperatureC, metar.AltimeterInHg);

        _logger.LogInformation($"Returning AirportConditionsDto for ICAO: {icao}");
        return new AirportConditionsDto(
            Icao: metar.Icao,
            Category: category,
            ObservedUtc: metar.Observed,
            WindDirDeg: metar.WindDirDeg,
            WindKt: metar.WindKt,
            GustKt: metar.GustKt,
            VisibilitySm: metar.VisibilitySm,
            CeilingFtAgl: metar.CeilingFtAgl,
            TemperatureC: metar.TemperatureC,
            DewpointC: metar.DewpointC,
            AltimeterInHg: metar.AltimeterInHg,
            HeadwindKt: head2,
            CrosswindKt: cross2,
            DensityAltitudeFt: da2
        );
    }
}
