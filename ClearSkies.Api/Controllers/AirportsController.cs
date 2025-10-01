using Microsoft.AspNetCore.Mvc;
using ClearSkies.Domain.Options;
using ClearSkies.Api.Services;
using ClearSkies.Api.Models;
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

        private readonly ClearSkies.Infrastructure.Airports.InMemoryAirportSearchCatalog _airportSearchCatalog;

        [HttpGet("search")]
        public IActionResult SearchAirports([FromQuery] SearchRequest request)
        {
            // Validate request parameters manually for custom error response
            if (string.IsNullOrWhiteSpace(request.Q))
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Invalid request parameters", 
                    Details = "Query parameter 'q' is required and cannot be empty" 
                });
            }

            if (request.Take < 1 || request.Take > 50)
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Invalid request parameters", 
                    Details = "take must be between 1 and 50" 
                });
            }

            if (request.Page < 1)
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Invalid request parameters", 
                    Details = "page must be greater than 0" 
                });
            }

            // Get all matching results first
            var allResults = _airportSearchCatalog.Search(request.Q, int.MaxValue).ToList();
            var totalResults = allResults.Count;

            // Apply pagination
            var pagedResults = allResults
                .Skip((request.Page - 1) * request.Take)
                .Take(request.Take)
                .Select(a => new AirportSearchResult
                {
                    Icao = a.Icao,
                    Iata = a.Iata,
                    Name = a.Name,
                    City = a.City,
                    State = a.State,
                    Country = a.Country
                })
                .ToList();

            var response = new SearchResponse<AirportSearchResult>
            {
                Items = pagedResults,
                Total = totalResults,
                Page = request.Page,
                PageSize = request.Take
            };

            return Ok(response);
        }


        private readonly IConditionsService _svc;
        private readonly ClearSkies.Domain.Aviation.IRunwayCatalog _runways;
        private readonly IAirportCatalog _catalog;
        private readonly WeatherOptions _opt;

        public AirportsController(
            IConditionsService svc,
            ClearSkies.Domain.Aviation.IRunwayCatalog runways,
            IAirportCatalog catalog,
            Microsoft.Extensions.Options.IOptions<WeatherOptions> opt,
            ClearSkies.Infrastructure.Airports.InMemoryAirportSearchCatalog airportSearchCatalog)
        {
            _svc = svc;
            _runways = runways;
            _catalog = catalog;
            _opt = opt.Value;
            _airportSearchCatalog = airportSearchCatalog;
        }

    // ...existing code...
// ...existing code...
        [HttpGet("{icao}/conditions")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetConditions(
            string icao,
            [FromQuery] string? runway = null,
            CancellationToken ct = default)
        {

            // 1) Validate ICAO format: must be 4 letters
            if (string.IsNullOrWhiteSpace(icao) || icao.Length != 4 || !icao.All(char.IsLetter))
            {
                return Problem(
                    title: "Invalid ICAO code",
                    detail: $"The ICAO code '{icao}' is not valid. Must be 4 letters.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: ProblemTypes.NoMetar);
            }
            // 2) If ICAO is not in catalog, return 404 (test and prod contract)
            // NOTE: If this is not hit, test setup/catalog may be wrong
            if (_catalog.GetElevationFt(icao) == null)
            {
                return Problem(
                    title: "Unknown ICAO code",
                    detail: $"The ICAO code '{icao}' is not recognized.",
                    statusCode: StatusCodes.Status404NotFound,
                    type: ProblemTypes.NoMetar);
            }

            // 3) Parse runway and look up heading
            int heading = 0;
            if (!string.IsNullOrWhiteSpace(runway))
            {
                // Try to get heading from runway catalog
                if (_runways.TryGetMagneticHeading(icao, runway, out int magneticHeadingDeg))
                {
                    heading = magneticHeadingDeg;
                }
                else
                {
                    return Problem(
                        title: "Invalid runway",
                        detail: $"Runway '{runway}' not found for airport '{icao}'.",
                        statusCode: StatusCodes.Status400BadRequest,
                        type: ProblemTypes.NoMetar);
                }
            }
            // 4) Call service with heading
            var dto = await _svc.GetConditionsAsync(icao, heading, ct);
            if (dto != null && !string.IsNullOrWhiteSpace(runway))
            {
                dto.Runway = runway;
            }
            if (dto is null)
            {
                return Problem(
                    title: "No METAR available",
                    detail: $"Upstream provider returned no observation for station '{icao.ToUpperInvariant()}'.",
                    statusCode: StatusCodes.Status502BadGateway,
                    type: ProblemTypes.NoMetar);
            }

            // 4) If runway is unknown, always set notice header (test and prod contract)
            if (!string.IsNullOrWhiteSpace(runway))
            {
                // Use GetAirportRunways to get all known runway designators for this ICAO
                var airportRunways = _runways.GetAirportRunways(icao);
                var runwaySet = airportRunways?.Runways.Select(r => r.Designator).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (runwaySet == null || !runwaySet.Contains(runway))
                {
                    Response.Headers["X-Runway-Notice"] = "Unknown runway";
                }
            }
            // Always set X-Cache header to reflect cache status for diagnostics
            if (!string.IsNullOrWhiteSpace(dto.CacheResult))
            {
                Response.Headers["X-Cache"] = dto.CacheResult;
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
                Response.Headers["Warning"] = "110 - \"Response is stale\"";
            }

            // 6. (For future write endpoints) Support preconditions (If-Match/412)
            // bool FailsIfMatch(string currentEtag) =>
            //     Request.Headers.TryGetValue("If-Match", out var im) && im != currentEtag;
            // if (FailsIfMatch(etag)) return StatusCode(StatusCodes.Status412PreconditionFailed);
            // --- End HTTP Caching Best Practices ---
// ...existing code...

            // Build stable ETag identity
            string stableIcao   = (dto.Icao ?? "").Trim().ToUpperInvariant();
            string observedIso  = dto.ObservedUtc.ToUniversalTime().ToString("O");
            string rawCanonical = (dto.RawMetar ?? "").Trim();
            string identity     = $"{stableIcao}|{observedIso}|{rawCanonical}";
            string hex          = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
            var computedEtag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{hex}\"", isWeak: true);

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
                            // Removed LogHeaders call: function no longer exists
                            return StatusCode(StatusCodes.Status304NotModified);
                        }
                    }
                }
            }

            // Removed LogHeaders call: function no longer exists
            return Ok(dto);
        }
    }
}