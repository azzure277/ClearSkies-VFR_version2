using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ClearSkies.Domain;
using ClearSkies.Domain.Options;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure;

namespace ClearSkies.Api.Services
{
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
            string? crosswindSide = null;
            if (cross < 0) crosswindSide = "left";
            else if (cross > 0) crosswindSide = "right";
            var fieldElevationFt = _catalog.GetElevationFt(metar.Icao) ?? 0;
            var da = AviationCalculations.DensityAltitudeFt(fieldElevationFt, metar.TemperatureC, metar.AltimeterInHg);

            var nowUtc = DateTime.UtcNow;
            var ageMinutes = (int)Math.Max(0, Math.Round((nowUtc - metar.Observed).TotalMinutes));
            // Fallback to 15 if StaleAfterMinutes is not present
            var staleAfter = 15;
            var optionsType = _options.GetType();
            var prop = optionsType.GetProperty("StaleAfterMinutes");
            if (prop != null)
            {
                var value = prop.GetValue(_options);
                if (value is int i && i > 0) staleAfter = i;
            }
            var isStale = ageMinutes >= staleAfter;
            if (_stamp != null && string.Equals(_stamp.Result, "FALLBACK", StringComparison.OrdinalIgnoreCase))
            {
                isStale = true;
            }

            _logger.LogInformation("Returning conditions for {ICAO} (DA {DA} ft, Head {Head} kt, Cross {Cross} kt)",
                icao, da, head, cross);

            return new AirportConditionsDto {
                Icao = metar.Icao,
                RawMetar = metar.RawMetar,
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
                CrosswindKt = Math.Abs(cross),
                CrosswindSide = crosswindSide,
                DensityAltitudeFt = da,
                IsStale = isStale,
                AgeMinutes = ageMinutes,
                CacheResult = metar.CacheResult,
                Runway = (runwayHeadingDeg > 0) ? null : null, // To be set by controller if needed
                RunwayHeadingDeg = (runwayHeadingDeg > 0) ? runwayHeadingDeg : (int?)null
            };
        }
    }
}
