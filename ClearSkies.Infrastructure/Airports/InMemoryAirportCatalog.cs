using System.Collections.Concurrent;

namespace ClearSkies.Infrastructure;

/// <summary>
/// Simple in-memory airport catalog (ICAO -> field elevation in feet MSL).
/// Extend this list anytime; no database required.
/// </summary>
public sealed class InMemoryAirportCatalog : IAirportCatalog
{
    // Approximate published field elevations (ft). Case-insensitive keys.
    private static readonly ConcurrentDictionary<string, int> _elev
        = new(StringComparer.OrdinalIgnoreCase)
        {
            // Seattle area
            ["KSEA"] = 433,
            ["KBFI"] = 21,
            ["KPAE"] = 606,

            // Bay Area
            ["KSFO"] = 13,
            ["KOAK"] = 9,
            ["KSJC"] = 62,

            // SoCal
            ["KLAX"] = 125,
            ["KSAN"] = 17,
            ["KBUR"] = 778,
            ["KSNA"] = 56,

            // Mountain / high elevation examples
            ["KDEN"] = 5434,
            ["KABQ"] = 5355,
            ["KASE"] = 7820,
            ["KPHX"] = 1135,
            ["KLAS"] = 2181,

            // Midwest / East Coast majors
            ["KDFW"] = 607,
            ["KORD"] = 672,
            ["KMSP"] = 841,
            ["KBOS"] = 20,
            ["KJFK"] = 13,
            ["KEWR"] = 18,
            ["KDCA"] = 15,
            ["KBWI"] = 146,
            ["KMIA"] = 8,
            ["KATL"] = 1026
        };

    public int? GetElevationFt(string icao)
    {
        if (string.IsNullOrWhiteSpace(icao)) return null;
        return _elev.TryGetValue(icao.Trim().ToUpperInvariant(), out var ft) ? ft : (int?)null;
    }
}
