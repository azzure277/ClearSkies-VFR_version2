<<<<<<< HEAD
using ClearSkies.Api.Options;
using ClearSkies.Api;
=======


using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
>>>>>>> master
using System.ComponentModel.DataAnnotations;
using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Runways;
<<<<<<< HEAD
using Microsoft.Extensions.Caching.Memory;

public partial class Program
{
    public static void Main(string[] args)
=======
using ClearSkies.Domain.Options; // Added for WeatherOptions
using ClearSkies.Infrastructure.Weather;

// ...existing code...
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
// Add ResponseCaching middleware for shared/proxy cache friendliness
builder.Services.AddResponseCaching();
// Register the normal weather provider
builder.Services.AddScoped<IWeatherProvider, InMemoryWeatherProvider>();
builder.Services.Decorate<IWeatherProvider, ClearSkies.Infrastructure.Weather.CachingWeatherProvider>();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services by type so DI supplies all constructor arguments automatically
builder.Services.AddSingleton<ClearSkies.Domain.Aviation.IRunwayCatalog, ClearSkies.Infrastructure.Runways.InMemoryRunwayCatalog>();
builder.Services.AddSingleton<IAirportCatalog, InMemoryAirportCatalog>();
builder.Services.AddHttpClient<IMetarSource, AvwxMetarSource>();

// Options binding (updated to use Configure)
builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));

// Let DI construct ConditionsService (no lambda!)
builder.Services.AddScoped<IConditionsService, ConditionsService>();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddMemoryCache(); // used in the next step (caching)
builder.Services.AddScoped<ClearSkies.Domain.Diagnostics.ICacheStamp, ClearSkies.Domain.Diagnostics.CacheStamp>();

var app = builder.Build();
// Add middleware to set X-Cache header for diagnostics
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var stamp = ctx.RequestServices.GetService<ClearSkies.Domain.Diagnostics.ICacheStamp>();
        if (!string.IsNullOrEmpty(stamp?.Result))
            ctx.Response.Headers["X-Cache"] = stamp.Result!;
        return Task.CompletedTask;
    });
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health check endpoint
app.MapGet("/health", (Microsoft.Extensions.Options.IOptions<ClearSkies.Domain.Options.WeatherOptions> opt) =>
{
    var o = opt.Value;
    return Results.Ok(new {
        status = "ok",
        weather = new {
            cacheMinutes = o.CacheMinutes,
            staleAfterMinutes = o.StaleAfterMinutes,
            criticallyStaleAfterMinutes = o.CriticallyStaleAfterMinutes
        },
        serverUtc = DateTime.UtcNow
    });
});

// Echo endpoint for ICAO
app.MapGet("/echo/{icao}", (string icao) => Results.Ok(new { icao }));


app.UseResponseCaching();
app.MapControllers();

app.Run();

app.MapGet("/airports/{icao}/runways",
    (string icao, ClearSkies.Domain.Aviation.IRunwayCatalog cat) =>
>>>>>>> master
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
