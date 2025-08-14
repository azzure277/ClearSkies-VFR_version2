namespace ClearSkies.Infrastructure;

using ClearSkies.Domain;
using System.Diagnostics.Metrics;

public interface IMetarSource
{
    Task<Metar?> GetLatestAsync(string icao, CancellationToken ct);
}
