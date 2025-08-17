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
                Icao: icao,
                Observed: DateTime.UtcNow.AddMinutes(-5),
                WindDirDeg: 180,
                WindKt: 10,
                GustKt: null,
                VisibilitySm: 10,
                CeilingFtAgl: 5000,
                TemperatureC: 29,
                DewpointC: 17,
                AltimeterInHg: 30.05m
            );
            return Task.FromResult<Metar?>(metar);
        }
    }
}
