using System;
using System.Threading.Tasks;
using ClearSkies.Domain;

namespace ClearSkies.Infrastructure.Weather
{
    public sealed class InMemoryWeatherProvider : IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, System.Threading.CancellationToken ct = default)
        {
            // Return a static METAR
            var metar = new Metar(
                icao: icao,
                observed: DateTime.UtcNow.AddMinutes(-5),
                windDirDeg: 180,
                windKt: 10,
                gustKt: null,
                visibilitySm: 10,
                ceilingFtAgl: 5000,
                temperatureC: 29,
                dewpointC: 17,
                altimeterInHg: 30.05m
            );
            return Task.FromResult<Metar?>(metar);
        }
    }
}
