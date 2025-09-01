using Microsoft.AspNetCore.Mvc;
using ClearSkies.Api.Options;
using ClearSkies.Api.Services;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;

namespace ClearSkies.Api.Controllers
{
    [ApiController]
    [Route("airports")]
    public sealed class AirportsController : ControllerBase
    {
        private readonly IConditionsService _svc;
        private readonly IRunwayCatalog _runways;
        private readonly ClearSkies.Api.Http.IEtagService _etagService;

        public AirportsController(IConditionsService svc, IRunwayCatalog runways, ClearSkies.Api.Http.IEtagService etagService)
        {
            _svc = svc;
            _runways = runways;
            _etagService = etagService;
        }

        [HttpGet("{icao}/conditions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AirportConditionsDto), StatusCodes.Status200OK)]
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