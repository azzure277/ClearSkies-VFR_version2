using Microsoft.AspNetCore.Mvc;
using ClearSkies.Api.Options;
using ClearSkies.Api.Services;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation; // If AirportConditionsDto is in ClearSkies.Domain

namespace ClearSkies.Api.Controllers
{
    [ApiController]
    [Route("airports")]
    public sealed class AirportsController : ControllerBase
    {
        private readonly IConditionsService _svc;
        private readonly IRunwayCatalog _runways;

        public AirportsController(IConditionsService svc, IRunwayCatalog runways)
        {
            _svc = svc;
            _runways = runways;
        }

        [HttpGet("{icao}/conditions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AirportConditionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
                return NotFound();

            return Ok(dto);
        }
    }
}