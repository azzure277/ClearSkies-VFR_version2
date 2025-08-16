using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClearSkies.App.Clean;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif

        // HttpClient for pages/services
        builder.Services.AddHttpClient();

        // Named client for your API base URL
        builder.Services.AddHttpClient("ClearSkiesApi", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5098/");
        });

        return builder.Build();
    }
}
