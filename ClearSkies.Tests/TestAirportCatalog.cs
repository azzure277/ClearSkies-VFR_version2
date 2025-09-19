using ClearSkies.Infrastructure;
using System.Collections.Generic;

namespace ClearSkies.Tests
{
    public class TestAirportCatalog : IAirportCatalog
    {
        private readonly Dictionary<string, int> _elevations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "KSFO", 13 },
            { "KJFK", 13 },
            { "KLAX", 125 },
            // Add more as needed for tests
        };

        public int? GetElevationFt(string icao)
        {
            if (_elevations.TryGetValue(icao, out var elev))
                return elev;
            // Only return elevation for known ICAOs; unknown ICAOs return null (to allow 404)
            return null;
        }
    }
}