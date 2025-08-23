#if DEBUG
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using ClearSkies.Domain; // Needed for IWeatherProvider

namespace ClearSkies.Infrastructure.Weather
{
    public sealed class ThrowingWeatherProvider : IWeatherProvider
    {
        public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            // Simulate upstream outage
            throw new HttpRequestException("Simulated upstream failure");
        }
    }
}
#endif
