using System;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
using ClearSkies.Infrastructure;

namespace ClearSkies.Tests
{
    public class TestMetarSource : IMetarSource
    {
        public Task<Metar?> GetLatestAsync(string icao, CancellationToken ct = default)
        {
            // Always return the same METAR for deterministic ETag
            var fixedObserved = new DateTime(2025, 8, 31, 12, 0, 0, DateTimeKind.Utc);
            var metar = new Metar(
                icao.Trim().ToUpperInvariant(),
                fixedObserved,
                190m, 12m, 18m, 10m, 4500, 20m, 12m, 30.02m
            );
            metar.RawMetar = "KSFO 311200Z 19012G18KT 10SM BKN045 20/12 A3002";
            return Task.FromResult<Metar?>(metar);
        }
    }
}