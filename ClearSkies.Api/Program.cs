
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using ClearSkies.Api.Services;
using ClearSkies.Infrastructure;
using ClearSkies.Domain;
using ClearSkies.Domain.Diagnostics;
using ClearSkies.Api.Http;
using ClearSkies.Api.Problems;
using ClearSkies.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services by type so DI supplies all constructor arguments automatically
builder.Services.AddSingleton<ClearSkies.Domain.Aviation.IRunwayCatalog, ClearSkies.Infrastructure.Runways.InMemoryRunwayCatalog>();
builder.Services.AddSingleton<ClearSkies.Infrastructure.Airports.InMemoryAirportCatalog>(); // for elevation
builder.Services.AddSingleton<ClearSkies.Infrastructure.Airports.InMemoryAirportSearchCatalog>(); // for search
builder.Services.AddSingleton<ClearSkies.Infrastructure.IAirportCatalog>(sp => sp.GetRequiredService<ClearSkies.Infrastructure.Airports.InMemoryAirportCatalog>());
builder.Services.AddSingleton<ClearSkies.Api.Http.IEtagService, ClearSkies.Api.Http.EtagService>();
builder.Services.AddHttpClient<IMetarSource, AvwxMetarSource>();
builder.Services.AddHttpContextAccessor();

// Register IConditionsService for DI
builder.Services.AddSingleton<IConditionsService, ConditionsService>();

// Register cache services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IConditionsCache, ConditionsCache>();

// Register ICacheStamp for DI
builder.Services.AddSingleton<ICacheStamp, CacheStamp>();

if (builder.Environment.EnvironmentName == "Testing" || builder.Configuration.GetValue<bool>("UseStaticTestCache"))
{
    builder.Services.AddSingleton<IMetarCache>(sp =>
        new StaticTestMetarCache(() => DateTime.UtcNow));
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IMetarCache, MemoryMetarCache>();
}

builder.Services.AddSingleton<ClearSkies.Infrastructure.MetarSourceWeatherProviderAdapter>(sp =>
    new ClearSkies.Infrastructure.MetarSourceWeatherProviderAdapter(
        sp.GetRequiredService<IMetarSource>()
    ));
builder.Services.AddSingleton<IWeatherProvider>(sp =>
    new ClearSkies.Infrastructure.Weather.CachingWeatherProvider(
        sp.GetRequiredService<ClearSkies.Infrastructure.MetarSourceWeatherProviderAdapter>()
    ));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

