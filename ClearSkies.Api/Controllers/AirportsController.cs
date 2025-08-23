
using Microsoft.AspNetCore.Mvc;
using ClearSkies.Domain.Options;
using ClearSkies.Api.Services;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Api.Problems;
using ClearSkies.Infrastructure;

namespace ClearSkies.Api.Controllers
{
    [ApiController]
    [Route("airports")]
    public sealed class AirportsController : ControllerBase
    {
    private readonly IConditionsService _svc;
    private readonly ClearSkies.Domain.Aviation.IRunwayCatalog _runways;
    private readonly IAirportCatalog _catalog;
    private readonly WeatherOptions _opt;

    public AirportsController(IConditionsService svc, ClearSkies.Domain.Aviation.IRunwayCatalog runways, IAirportCatalog catalog, Microsoft.Extensions.Options.IOptions<WeatherOptions> opt)
        {
            _svc = svc;
            _runways = runways;
            _catalog = catalog;
            _opt = opt.Value;
        }

        [HttpGet("{icao}/conditions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AirportConditionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetConditions(
            string icao,
            [FromQuery] string? runway = null,
            CancellationToken ct = default)
        {
            // 1) Validate ICAO format
            // ...existing validation code...

            // 2) Call service
            // Use heading=0 as default; update if you want to parse from runway
            var dto = await _svc.GetConditionsAsync(icao, 0, ct);
            if (dto is null)
            {
                return Problem(
                    title: "No METAR available",
                    detail: $"Upstream provider returned no observation for station '{icao.ToUpperInvariant()}'.",
                    statusCode: StatusCodes.Status502BadGateway,
                    type: ProblemTypes.NoMetar);
            }

            // --- HTTP Caching Best Practices ---
            // 1. Set Cache-Control and Vary headers
            var maxAge = TimeSpan.FromMinutes(Math.Max(1, _opt.CacheMinutes));
            Response.Headers["Cache-Control"] = $"public, max-age={(int)maxAge.TotalSeconds}";
            Response.Headers["Vary"] = "Accept";

            // 2. Use weak ETag
            var etag = $"W/\"{icao}-{dto.ObservedUtc:O}\"";
            Response.Headers["ETag"] = etag;
            Response.Headers["Last-Modified"] = dto.ObservedUtc.ToString("R");

            // 3. ETag/If-None-Match check (prefer ETag)
            if (Request.Headers.TryGetValue("If-None-Match", out var inm) &&
                inm.ToString().Replace("W/", "", StringComparison.OrdinalIgnoreCase).Trim('"') ==
                etag.Replace("W/", "", StringComparison.OrdinalIgnoreCase).Trim('"'))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            // 4. If-Modified-Since check (only if no ETag)
            if (!Request.Headers.ContainsKey("If-None-Match") &&
                Request.Headers.TryGetValue("If-Modified-Since", out var ims) &&
                DateTime.TryParse(ims, out var since) &&
                dto.ObservedUtc <= since.ToUniversalTime())
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            // 5. Add Warning header if serving fallback/stale data
            if (dto.CacheResult == "FALLBACK")
            {
                Response.Headers["X-Cache-Fallback"] = "true";
                Response.Headers.Append("Warning", "110 - \"Response is stale\"");
            }

            // 6. (For future write endpoints) Support preconditions (If-Match/412)
            // bool FailsIfMatch(string currentEtag) =>
            //     Request.Headers.TryGetValue("If-Match", out var im) && im != currentEtag;
            // if (FailsIfMatch(etag)) return StatusCode(StatusCodes.Status412PreconditionFailed);
            // --- End HTTP Caching Best Practices ---

            return Ok(dto);
        }
    }
}