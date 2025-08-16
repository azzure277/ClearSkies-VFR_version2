using System.Net.Http.Json;
using ClearSkies.App.Models;

namespace ClearSkies.App
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        private const string ApiBase = "https://localhost:44344";
        private static readonly HttpClient _http = new();

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void Fetch_Clicked(object sender, EventArgs e)
        {
            // Fix: Use 'this.IcaoEntry', 'this.RunwayPicker', etc. since they are defined in XAML with x:Name
            var icao = (this.IcaoEntry.Text ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(icao))
            {
                await DisplayAlert("Missing ICAO", "Enter an ICAO like KSFO or KDEN.", "OK");
                return;
            }

            string? runway = null;
            if (this.RunwayPicker.SelectedItem is not null)
            {
                var item = this.RunwayPicker.SelectedItem;
                var valueProp = item.GetType().GetProperty("Value");
                if (valueProp is not null)
                    runway = valueProp.GetValue(item)?.ToString();
                runway ??= item.ToString();
            }
            if (string.IsNullOrWhiteSpace(runway))
            {
                await DisplayAlert("Pick a runway", "Please select a runway first.", "OK");
                return;
            }

            try
            {
                var url = $"{ApiBase}/airports/{icao}/conditions?runway={Uri.EscapeDataString(runway)}";
                var dto = await _http.GetFromJsonAsync<AirportConditionsDto>(url);

                if (dto is null)
                {
                    this.ResultLabel.Text = "No data returned.";
                    this.StaleBadge.IsVisible = false;
                    return;
                }

                this.StaleBadge.IsVisible = dto.IsStale;
                this.StaleBadge.Text = dto.IsStale ? $"STALE ({dto.AgeMinutes} min)" : "STALE";

                this.ResultLabel.Text =
                    $"ICAO: {dto.Icao}\n" +
                    $"Cat: {dto.Category}  |  Obs: {dto.ObservedUtc:yyyy-MM-dd HH:mm}Z  ({dto.AgeMinutes} min old)\n" +
                    $"Wind: {dto.WindDirDeg:0}° @ {dto.WindKt:0} kt  (G{dto.GustKt?.ToString("0") ?? "—"})\n" +
                    $"Vis: {dto.VisibilitySm:0} sm  |  Ceiling: {(dto.CeilingFtAgl?.ToString() ?? "—")} ft\n" +
                    $"Headwind: {dto.HeadwindKt:0.#} kt  |  Crosswind: {dto.CrosswindKt:0.#} kt\n" +
                    $"DA: {dto.DensityAltitudeFt:N0} ft  |  Stale: {dto.IsStale}";
            }
            catch (HttpRequestException)
            {
                await DisplayAlert("Network error", "Could not fetch conditions. Check API is running and URL/port.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
