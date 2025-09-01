
using Microsoft.AspNetCore.Mvc;
using ClearSkies.Domain.Options;
using ClearSkies.Api.Services;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
<<<<<<< HEAD
=======
using ClearSkies.Api.Problems;
using ClearSkies.Infrastructure;
>>>>>>> master

namespace ClearSkies.Api.Controllers
{
    [ApiController]
    [Route("airports")]
    public sealed class AirportsController : ControllerBase
    {
<<<<<<< HEAD
        private readonly IConditionsService _svc;
        private readonly IRunwayCatalog _runways;
        private readonly ClearSkies.Api.Http.IEtagService _etagService;

        public AirportsController(IConditionsService svc, IRunwayCatalog runways, ClearSkies.Api.Http.IEtagService etagService)
        {
            _svc = svc;
            _runways = runways;
            _etagService = etagService;
=======
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
>>>>>>> master
        }

        [HttpGet("{icao}/conditions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AirportConditionsDto), StatusCodes.Status200OK)]
<<<<<<< HEAD
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConditions(string icao, [FromQuery] string? runway = null, CancellationToken ct = default)
        {
            // Local function for diagnostic logging
            void LogHeaders(string context)
            {
                var etagVal = Response.Headers.ContainsKey("ETag") ? Response.Headers["ETag"].ToString() : "<none>";
                var cacheVal = Response.Headers.ContainsKey("X-Cache-Present") ? Response.Headers["X-Cache-Present"].ToString() : "<none>";
                System.Diagnostics.Debug.WriteLine($"[DIAG] {context}: ETag={etagVal}, X-Cache-Present={cacheVal}");
            }

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
            {
                LogHeaders("404/NotFound");
                return NotFound();
            }
=======
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
>>>>>>> master

            // Build stable ETag identity
            string stableIcao   = (dto.Icao ?? "").Trim().ToUpperInvariant();
            string observedIso  = dto.ObservedUtc.ToUniversalTime().ToString("O");
            string rawCanonical = (dto.RawMetar ?? "").Trim();
            string identity     = $"{stableIcao}|{observedIso}|{rawCanonical}";
            string hex          = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
            var etag            = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{hex}\"", isWeak: true);

            // Always set ETag header before any return
            // Return 304 NotModified if ETag matches If-None-Match BEFORE writing any response
            if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch))
            {
                // Compare ETag values ignoring weak/strong and quotes
                var normalizedIfNoneMatch = ifNoneMatch.ToString().Replace("W/", "").Replace("\"", "").Trim();
                var normalizedEtag = etag.ToString().Replace("W/", "").Replace("\"", "").Trim();
                if (normalizedIfNoneMatch == normalizedEtag)
                {
                    Response.Headers["ETag"] = etag.ToString();
                    // Forward custom headers from provider to API response
                    var ctxHeaders = Response.Headers;
                    var providerHeaders = HttpContext.Response.Headers;
                    foreach (var key in providerHeaders.Keys)
                    {
                        if ((key == "X-Cache-Present" || key == "Warning") && !ctxHeaders.ContainsKey(key))
                        {
                            ctxHeaders[key] = providerHeaders[key];
                        }
                    }
                    return new StatusCodeResult(StatusCodes.Status304NotModified);
                }
            }
            Response.Headers.ETag = etag.ToString();
            // Forward custom headers from provider to API response
            var ctxHeaders2 = Response.Headers;
            var providerHeaders2 = HttpContext.Response.Headers;
            foreach (var key in providerHeaders2.Keys)
            {
                if ((key == "X-Cache-Present" || key == "Warning") && !ctxHeaders2.ContainsKey(key))
                {
                    ctxHeaders2[key] = providerHeaders2[key];
                }
            }

            // If-None-Match check (weak)
            if (Request.Headers.TryGetValue(Microsoft.Net.Http.Headers.HeaderNames.IfNoneMatch, out var vals) && vals.Count > 0)
            {
                foreach (var v in vals)
                {
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    foreach (var raw in v.Split(','))
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        if (Microsoft.Net.Http.Headers.EntityTagHeaderValue.TryParse(raw.Trim(), out var clientTag) &&
                            clientTag.Equals(etag))
                        {
                            LogHeaders("304/NotModified");
                            return StatusCode(StatusCodes.Status304NotModified);
                        }
                    }
                }
            }

            LogHeaders("200/OK final");
            return Ok(dto);
        }
    }
}