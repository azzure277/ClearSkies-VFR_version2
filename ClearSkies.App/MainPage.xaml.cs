using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ClearSkies.App.Models;

namespace ClearSkies.App;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _http;

    // Use named client 'ClearSkiesApi' from MauiProgram
    public MainPage(IHttpClientFactory httpFactory)
    {
        InitializeComponent();
        _http = httpFactory.CreateClient("ClearSkiesApi");
    }

    private async void OnFetchClicked(object sender, EventArgs e)
    {
        var icao = IcaoEntry?.Text?.Trim().ToUpperInvariant();
        var runway = RunwayEntry?.Text?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(icao))
        {
            await DisplayAlert("Input required", "Enter an ICAO (e.g., KSFO).", "OK");
            return;
        }

        try
        {
            // runway is a string designator like "28L" (NOT an int), matching your API
            var url = string.IsNullOrWhiteSpace(runway)
                ? $"airports/{icao}/conditions"
                : $"airports/{icao}/conditions?runway={Uri.EscapeDataString(runway)}";

            var dto = await _http.GetFromJsonAsync<AirportConditionsDto>(url);

            if (dto is null)
            {
                ResultLabel.Text = "No data returned from API.";
                StaleBadge.IsVisible = false;
                return;
            }

            // Simple render
            ResultLabel.Text =
                $"ICAO: {dto.Icao}\n" +
                $"Category: {dto.Category}\n" +
                $"Observed (UTC): {dto.ObservedUtc:yyyy-MM-dd HH:mm}\n" +
                $"Wind: {dto.WindDirDeg}° @ {dto.WindKt} kt (G{dto.GustKt?.ToString() ?? "-"})\n" +
                $"Vis: {dto.VisibilitySm} sm  Ceiling: {(dto.CeilingFtAgl?.ToString() ?? "-") } ft AGL\n" +
                $"Temp/Dew: {dto.TemperatureC}°C / {dto.DewpointC}°C  Alt: {dto.AltimeterInHg} inHg\n" +
                $"Headwind: {dto.HeadwindKt} kt  Crosswind: {dto.CrosswindKt} kt\n" +
                $"Density Altitude: {dto.DensityAltitudeFt} ft\n" +
                $"Age: {dto.AgeMinutes} min";

            StaleBadge.Text = dto.IsStale ? "STALE" : "FRESH";
            StaleBadge.BackgroundColor = dto.IsStale ? Colors.OrangeRed : Colors.SeaGreen;
            StaleBadge.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            ResultLabel.Text = $"HTTP error: {ex.Message}";
            StaleBadge.IsVisible = false;
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"Error: {ex.Message}";
            StaleBadge.IsVisible = false;
        }
    }
}
