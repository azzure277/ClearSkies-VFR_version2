using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
// Removed unnecessary using

namespace ClearSkies.Infrastructure;

public sealed class AvwxMetarSource : IMetarSource
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public AvwxMetarSource(HttpClient http)
    {
        _http = http;
        _apiKey = Environment.GetEnvironmentVariable("AVWX_API_KEY") ?? "";
    }

    public async Task<Metar?> GetLatestAsync(string icao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        icao = (icao ?? string.Empty).Trim().ToUpperInvariant();
        if (icao.Length == 0) return null;

        var url = $"https://avwx.rest/api/metar/{icao}?options=&format=json";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Authorization", _apiKey);
        req.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            // Minimal parse for stub
            // TODO: Parse JSON properly
            return new Metar(icao, DateTime.UtcNow, 190m, 12m, 18m, 10m, 4500, 20m, 12m, 30.02m);
        }
        catch
        {
            return null;
        }
    }
}
