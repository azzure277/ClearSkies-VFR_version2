
using Microsoft.AspNetCore.Mvc;
using ClearSkies.Domain.Options;
using ClearSkies.Api.Services;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Api.Problems;

namespace ClearSkies.Api.Controllers
{
    [ApiController]
    [Route("airports")]
    public sealed class AirportsController : ControllerBase
    {
        private readonly IConditionsService _svc;
        private readonly IRunwayCatalog _runways;
        private readonly WeatherOptions _opt;

    public AirportsController(IConditionsService svc, IRunwayCatalog runways, Microsoft.Extensions.Options.IOptions<WeatherOptions> opt)
        {
            _svc = svc;
            _runways = runways;
            _opt = opt.Value;
        }

        [HttpGet("{icao}/conditions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AirportConditionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetConditions(
            string icao,
            [FromQuery] string? runway = null,
            CancellationToken ct = default)
        {
            int? runwayMagHeading = null;

            if (!string.IsNullOrWhiteSpace(runway))
            {
                if (!_runways.TryGetMagneticHeading(icao, runway, out var hdg))
                {
                    return NotFound(new { message = $"Runway '{runway}' not found for {icao}." });
                }

                runwayMagHeading = hdg;
            }

            var dto = await _svc.GetConditionsAsync(icao, runwayMagHeading ?? 0, ct);

            if (dto is null)
                return Problem(
                    title: "No METAR available",
                    detail: $"Upstream provider returned no observation for station '{icao}'.",
                    statusCode: StatusCodes.Status502BadGateway,
                    type: "https://clear-skies.dev/problems/no-metar");

            if (!string.IsNullOrEmpty(dto.CacheResult))
                Response.Headers["X-Cache"] = dto.CacheResult;

            // Add stale/critically stale headers for UI/clients
            if (dto.IsStale)
                Response.Headers["X-Data-Stale"] = $"true; age={dto.AgeMinutes}m; threshold={_opt.StaleAfterMinutes}m";

            if (_opt.CriticallyStaleAfterMinutes is int crit && dto.AgeMinutes >= crit)
                Response.Headers.Append("Warning", $"110 - \"METAR critically stale ({dto.AgeMinutes} min)\"");
            return Ok(dto);
        }
    }
}