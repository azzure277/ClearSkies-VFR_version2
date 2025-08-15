using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IRunwayCatalog, InMemoryRunwayCatalog>();
// Register the AirportCatalog implementation
builder.Services.AddSingleton<IAirportCatalog, InMemoryAirportCatalog>();
// Register the MetarSource implementation

// AVWX ONLY during debug
builder.Services.AddHttpClient<IMetarSource, AvwxMetarSource>();

// Conditions service with logging
builder.Services.AddScoped<IConditionsService, ConditionsService>(sp =>
    new ConditionsService(
        sp.GetRequiredService<IMetarSource>(),
        sp.GetRequiredService<IAirportCatalog>(),
        sp.GetRequiredService<ILogger<ConditionsService>>()
    )
);
// Add controllers
builder.Services.AddControllers();

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

// GET /airports/{icao}/conditions?runwayHeadingDeg=160
app.MapGet("/airports/{icao}/conditions",
    async (string icao,
            int? runwayHeadingDeg,
            string? runway,
            IRunwayCatalog runwayCatalog,
            IConditionsService svc,
            CancellationToken ct) =>
    {
        int heading;

        if (!string.IsNullOrWhiteSpace(runway))
        {
            var lookedUp = runwayCatalog.GetHeadingDeg(icao, runway);
            if (lookedUp is null)
                return Results.BadRequest($"Unknown runway '{runway}' for {icao}.");
            heading = lookedUp.Value;
        }
        else
        {
            heading = runwayHeadingDeg.GetValueOrDefault(160);
        }

        var dto = await svc.GetConditionsAsync(icao, heading, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    })
    .WithTags("Airport Conditions");


app.MapControllers();
app.Run();

app.MapGet("/airports/{icao}/runways",
    (string icao, IRunwayCatalog cat) =>
    {
        var list = cat.GetRunwayHeadings(icao);
        return list.Count == 0
            ? Results.NotFound()
            : Results.Ok(list.Select(x => new { runway = x.Runway, headingDeg = x.HeadingDeg }));
    })
   .WithTags("Airport Runways");


// dotnet clean c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet build c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet run --project c:\Users\brand\source\repos\ClearSkies\ClearSkies.Api\ClearSkies.Api.csproj
