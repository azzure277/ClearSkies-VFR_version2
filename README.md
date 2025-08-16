ClearSkies VFR is a Microsoft-stack C# application that helps pilots quickly determine if current and forecasted conditions support safe Visual Flight Rules (VFR) flying. The app retrieves real-time aviation weather (METAR/TAF), classifies flight category (VFR, MVFR, IFR, LIFR), calculates density altitude, and performs crosswind/headwind checks for selected runways. Built with .NET 8, ASP.NET Core, .NET MAUI, Entity Framework Core, and Azure, its purpose is to provide a quick, accurate, and user-friendly tool for pilots to assess weather conditions at a glance. Pilots often need to check multiple sources for METAR, TAF, crosswind, and density altitude data; ClearSkies consolidates these into a single instant report. Primary users include general aviation pilots, student pilots, flight instructors, and aviation enthusiasts. Core features include live METAR/TAF retrieval from NOAA and AVWX REST API with global ICAO support, automatic flight category classification based on FAA thresholds, density altitude calculation using airport-specific field elevation, crosswind and headwind component calculation from runway headings, a runway catalog preloaded with major airports, and planned weather alerts and multi-platform UI. The architecture follows a clean separation of concerns: ClearSkies.Domain holds business logic, models, and calculations; ClearSkies.Infrastructure handles data retrieval and the airport catalog; ClearSkies.Api serves endpoints like /airports/{icao}/conditions in JSON; ClearSkies.App (planned) will provide a MAUI cross-platform front-end; Azure integration (planned) will host the API, store data in Azure SQL, and send alerts via Functions. Locally, you can build with .NET 8, run the API, and access Swagger UI or test with curl. Example output includes ICAO, category, observed time, wind details, visibility, ceiling, temperature, altimeter, headwind, crosswind, and density altitude. The roadmap includes completing the MAUI UI, adding route planning, METAR history, and deploying to Azure

What the system is (high level)
Clients (MAUI app, curl, Swagger) call a single endpoint:
GET /airports/{icao}/conditions?runway=XX[LR/C]
ASP.NET Core API hosts the endpoint and coordinates all work.
Domain layer does the aviation math (flight category, wind components, density altitude).
Data providers fetch raw weather (METAR now; TAF coming) and airport/runway metadata.
DTOs shape the output, including freshness fields: isStale, ageMinutes.
Request → Response (happy path)
Request comes in with an ICAO (e.g., KDEN) and optional runway (17L).
Airport & runway lookup
We resolve airport elevation (for density altitude) via an AirportCatalog.
We resolve runway magnetic heading via a RunwayCatalog (in-memory now; being expanded).
Weather fetch (METAR)
A WeatherProvider abstraction fetches the latest METAR for the ICAO.
We parse obs time, wind (dir/speed/gust), temp/dewpoint, altimeter, visibility, cloud layers, and remarks.
Freshness check
We compute ageMinutes (now − observation time) and set isStale if over a configurable threshold.
Aviation calculations (domain)
Wind components: using wind direction vs runway heading → headwind/tailwind and crosswind (with sign).
Flight category: VFR/MVFR/IFR/LIFR based on visibility & ceiling.
Density altitude: uses elevation, temperature, and pressure (from altimeter) to estimate DA.
Response shaping
We return an AirportConditionsDto with: ICAO, runway used, headings, wind components, flight category, DA, METAR raw bits, ageMinutes, isStale.
Current components (what exists today)
Endpoint: /airports/{icao}/conditions with ?runway=XX[LR/C].
AirportCatalog (in-memory): ICAO → elevation ft (used for DA).
RunwayCatalog (in-memory, WIP): ICAO → runways (designation → magnetic heading).
WeatherProvider (pluggable): currently a single implementation to fetch the latest METAR per airport.
Domain services:
WindComponentCalculator
FlightCategoryCalculator
DensityAltitudeCalculator
DTO: AirportConditionsDto now includes isStale and ageMinutes.
Verification: tested via Swagger/cURL (KSFO, KDEN).
Where METARs come from (and how)
We abstract this behind IWeatherProvider so we can swap sources without touching domain logic. Typical sources we can (and often do) use:
NOAA/FAA aviation data services that expose METARs as JSON/XML.
Local cache to avoid hammering upstream for the same airport repeatedly.
Fetch flow today (conceptually):
Build request for latest METAR for the ICAO.
Parse response → internal Metar model.
Surface timestamp (UTC), wind (direction/speed/gust), visibility, ceilings, temp/dewpoint, altimeter.
Planned robustness:
Retry + backoff (Polly) for transient network failures.
Circuit breaker if the upstream is unhealthy.
Graceful fallback (serve cached/stale data and flag isStale=true).
Freshness & caching (design we’re implementing)
Freshness: configurable threshold (e.g., 20–30 min). Older than threshold → isStale=true.
Cache: in-process IMemoryCache keyed by ICAO (and data type: METAR vs TAF), with TTL ~5 minutes.
Reduces latency and upstream calls.
Cache entry contains both the parsed model and the raw text (handy for debugging).
Runway resolution (why it matters)
Clients can pass runway=28L instead of a heading in degrees.
We map Kxxx + "28L" → magnetic heading (e.g., 284°).
Domain math uses this heading to compute headwind and crosswind correctly.
This removes error-prone client-side heading inputs and gives consistent results.
Calculations (brief but precise)
Headwind / Crosswind
Angle = difference between wind direction and runway heading (handle wrap-around).
Headwind = windSpeed × cos(angle). Crosswind = windSpeed × sin(angle) (signed for left/right).
Flight Category (typical thresholds)
VFR: vis ≥ 5 sm and ceiling ≥ 3000 ft
MVFR: vis 3–5 sm or ceiling 1000–3000 ft
IFR: vis 1–3 sm or ceiling 500–1000 ft
LIFR: vis < 1 sm or ceiling < 500 ft
Density Altitude (engineering estimate)
Pressure altitude from altimeter & field elevation; correct for temperature vs ISA → DA in feet.
Error handling (user-facing behavior)
If ICAO is unknown: 404 with guidance.
If METAR unavailable:
Return last cached obs (if present) with isStale=true, or
502/503 with a clear message that upstream weather is unavailable.
Validation: runway format, ICAO format, query combos.
Observability & ops (how we’ll run it)
Hosting: ASP.NET Core API on Azure App Service (or containerized to AKS later).
Config: Key vault/app settings for API keys, thresholds, cache TTL.
Monitoring: Application Insights traces/metrics; logs include ICAO, latency, provider outcome.
Security: API key or Entra ID (OAuth2) for protected deployments; rate limiting to protect upstream.
Test plan (what we test)
Unit tests:
RunwayCatalog lookups (e.g., KDEN 17L → ~174–176° depending on dataset).
Wind component edge cases (calm winds, direct crosswind, wrap-around at 360°).
Flight category boundary conditions (exact thresholds).
DA sanity checks (hot/high increases DA).
Integration tests:
KDEN?runway=17L returns sensible crosswind/headwind & DA.
Stale logic flips after the threshold.
Caching lowers provider call count.
Roadmap (immediate next)
Expand RunwayCatalog (authoritative data; handle L/R/C; magnetic variation drift over time can be a later enhancement).
Introduce caching + retry policy (IMemoryCache + Polly).
TAF provider (trend/next-6hr window) and minimal trend badge in DTO.
Configurable freshness threshold surfaced via appsettings and exposed in /health or /info.
