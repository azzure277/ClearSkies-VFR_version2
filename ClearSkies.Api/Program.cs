using ClearSkies.Api.Options;
using System.ComponentModel.DataAnnotations;
using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Runways;
using ClearSkies.Domain.Options; // Added for WeatherOptions
using ClearSkies.Infrastructure.Weather;

var builder = WebApplication.CreateBuilder(args);
// Register the stub provider
builder.Services.AddSingleton<IWeatherProvider, InMemoryWeatherProvider>();
// Decorate with caching
builder.Services.Decorate<IWeatherProvider, CachingWeatherProvider>();

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
app.Run();

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


// dotnet clean c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet build c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet run --project c:\Users\brand\source\repos\ClearSkies\ClearSkies.Api\ClearSkies.Api.csproj
