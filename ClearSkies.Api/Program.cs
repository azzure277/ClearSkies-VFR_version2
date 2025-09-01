using ClearSkies.Api.Options;
using ClearSkies.Api;
using System.ComponentModel.DataAnnotations;
using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Runways;
using Microsoft.Extensions.Caching.Memory;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Register services by type so DI supplies all constructor arguments automatically
        builder.Services.AddSingleton<ClearSkies.Domain.Aviation.IRunwayCatalog, ClearSkies.Infrastructure.Runways.InMemoryRunwayCatalog>();
        builder.Services.AddSingleton<IAirportCatalog, InMemoryAirportCatalog>();

    // Register EtagService for ETag computation
    builder.Services.AddSingleton<ClearSkies.Api.Http.IEtagService, ClearSkies.Api.Http.EtagService>();

        // Register the inner AvwxMetarSource for use by the cache
        builder.Services.AddHttpClient<AvwxMetarSource>();

        // Register IHttpContextAccessor for header logic
        builder.Services.AddHttpContextAccessor();

        // Register CachingWeatherProvider as the IMetarSource (singleton for shared cache/provider)
        builder.Services.AddSingleton<IMetarSource>(sp =>
            new CachingWeatherProvider(
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<AvwxMetarSource>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                sp.GetRequiredService<ClearSkies.Api.Http.IEtagService>()
            ));

        // Options binding
        builder.Services
            .AddOptions<WeatherOptions>()
            .Bind(builder.Configuration.GetSection("Weather"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Let DI construct ConditionsService (no lambda!)
        builder.Services.AddScoped<IConditionsService, ConditionsService>();

        // Add controllers
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok("OK"));

        // Echo endpoint for ICAO
        app.MapGet("/echo/{icao}", (string icao) => Results.Ok(new { icao }));

        app.MapControllers();
        app.MapGet("/airports/{icao}/runways",
            (string icao, ClearSkies.Domain.Aviation.IRunwayCatalog cat) =>
            {
                var arpt = cat.GetAirportRunways(icao);
                if (arpt == null || arpt.Runways == null || arpt.Runways.Count == 0)
                    return Results.NotFound();
                var list = arpt.Runways.Select(x => new { runway = x.Designator, headingDeg = x.MagneticHeadingDeg });
                return Results.Ok(list);
            })
           .WithTags("Airport Runways");

        app.Run();
    }
}
