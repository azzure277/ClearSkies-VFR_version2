using ClearSkies.Domain;
using System.Globalization;
using System.Net.Http;
using System.Xml.Linq;

namespace ClearSkies.Infrastructure;

public sealed class NoaaMetarSource : IMetarSource
{
    private readonly HttpClient _http;
    public NoaaMetarSource(HttpClient http) => _http = http;

    public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct)
    {
        icao = (icao ?? string.Empty).Trim().ToUpperInvariant();
        if (icao.Length == 0) return null;

        var url =
            "https://aviationweather.gov/adds/dataserver_current/httpparam?" +
            "dataSource=metars&requestType=retrieve&format=XML" +
            $"&stationString={icao}&hoursBeforeNow=2&mostRecent=true";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("ClearSkiesVFR/1.0 (+https://localhost)");
            req.Headers.Accept.ParseAdd("application/xml");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = XDocument.Load(stream);

            var metarEl = doc.Descendants("METAR").FirstOrDefault();
            if (metarEl is null) return null;

            static decimal? D(string? s) =>
                decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
            static int? I(string? s) =>
                int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

            var obsTimeStr = metarEl.Element("observation_time")?.Value;
            var observed = DateTime.TryParse(
                obsTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var t)
                ? t : DateTime.UtcNow;

            var windDir = D(metarEl.Element("wind_dir_degrees")?.Value) ?? 0m;
            var windKt = D(metarEl.Element("wind_speed_kt")?.Value) ?? 0m;
            var gustKt = D(metarEl.Element("wind_gust_kt")?.Value);
            var visSm = D(metarEl.Element("visibility_statute_mi")?.Value) ?? 0m;
            var tempC = D(metarEl.Element("temp_c")?.Value) ?? 0m;
            var dewC = D(metarEl.Element("dewpoint_c")?.Value) ?? 0m;
            var altInHg = D(metarEl.Element("altim_in_hg")?.Value) ?? 29.92m;

            int? ceilingFtAgl = metarEl
                .Elements("sky_condition")
                .Select(el => new
                {
                    cover = el.Attribute("sky_cover")?.Value,
                    baseFt = I(el.Attribute("cloud_base_ft_agl")?.Value)
                })
                .Where(x => x.baseFt.HasValue && (x.cover == "BKN" || x.cover == "OVC"))
                .Select(x => x.baseFt!.Value)
                .DefaultIfEmpty()
                .Min();

            return new Metar(
                icao,
                observed,
                windDir,
                windKt,
                gustKt,
                visSm,
                ceilingFtAgl,
                tempC,
                dewC,
                altInHg
            );
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
