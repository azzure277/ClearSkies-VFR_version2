using System.Threading;
using System.Threading.Tasks;

namespace ClearSkies.Domain
{
    public interface IWeatherProvider
    {
        Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default);
    }
}
