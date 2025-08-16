using ClearSkies.App.Clean.Models;
using System.Net.Http.Json;

namespace ClearSkies.App.Clean;

public partial class MainPage : ContentPage
{
    private readonly IHttpClientFactory _httpFactory;

    public MainPage(IHttpClientFactory httpFactory)
    {
        InitializeComponent();
        _httpFactory = httpFactory;
    }

    private async void OnGetConditionsClicked(object sender, EventArgs e)
    {
        var client = _httpFactory.CreateClient("ClearSkiesApi");

        var icao = IcaoEntry.Text?.Trim().ToUpper() ?? "KSFO";
        var runway = RunwayEntry.Text?.Trim();

        var url = $"airports/{icao}/conditions";
        if (!string.IsNullOrEmpty(runway))
            url += $"?runway={runway}";

        try
        {
            var dto = await client.GetFromJsonAsync<AirportConditionsDto>(url);
            if (dto != null)
            {
                ResultLabel.Text = $"{dto.Icao}: {dto.Category} (VFR=3)\n" +
                                   $"Wind {dto.WindDirDeg}°/{dto.WindKt}kt, Gust {dto.GustKt}\n" +
                                   $"Vis {dto.VisibilitySm}sm, Ceiling {dto.CeilingFtAgl}ft";
            }
            else
            {
                ResultLabel.Text = "No data returned.";
            }
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"Error: {ex.Message}";
        }
    }
}
