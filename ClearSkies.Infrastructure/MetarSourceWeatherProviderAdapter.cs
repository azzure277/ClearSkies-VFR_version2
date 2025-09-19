using ClearSkies.Domain;
using System.Threading.Tasks;

namespace ClearSkies.Infrastructure
{
    /// <summary>
    /// Adapter to allow an IMetarSource to be used as an IWeatherProvider.
    /// </summary>
    public class MetarSourceWeatherProviderAdapter : IWeatherProvider
    {
        private readonly IMetarSource _metarSource;

        public MetarSourceWeatherProviderAdapter(IMetarSource metarSource)
        {
            _metarSource = metarSource;
        }

        public async Task<Metar?> GetMetarAsync(string icao, System.Threading.CancellationToken ct = default)
        {
            // Delegate to the IMetarSource implementation
            return await _metarSource.GetLatestAsync(icao, ct);
        }
    }
}