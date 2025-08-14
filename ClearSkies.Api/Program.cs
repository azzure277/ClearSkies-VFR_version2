using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AVWX ONLY during debug
builder.Services.AddHttpClient<IMetarSource, AvwxMetarSource>();

// Conditions service with logging
builder.Services.AddScoped<IConditionsService, ConditionsService>(sp =>
    new ConditionsService(
        sp.GetRequiredService<IMetarSource>(),
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
    async (string icao, int? runwayHeadingDeg, IConditionsService svc, ILoggerFactory loggerFactory, CancellationToken ct) =>
    {
        var logger = loggerFactory.CreateLogger("AirportConditionsEndpoint");
        logger.LogInformation($"Request for ICAO: {icao}, Runway Heading: {runwayHeadingDeg}");
        var heading = runwayHeadingDeg.GetValueOrDefault(160);
        var dto = await svc.GetConditionsAsync(icao, heading, ct);
        if (dto is null)
        {
            logger.LogWarning($"No conditions found for ICAO: {icao} with heading {heading}");
            return Results.NotFound();
        }
        logger.LogInformation($"Returning conditions for ICAO: {icao}");
        return Results.Ok(dto);
    })
    .WithTags("Airport Conditions"); // keeps things tidy in Swagger

app.MapControllers();
app.Run();

// dotnet clean c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet build c:\Users\brand\source\repos\ClearSkies\ClearSkies.sln
// dotnet run --project c:\Users\brand\source\repos\ClearSkies\ClearSkies.Api\ClearSkies.Api.csproj
