using System.Collections.Concurrent;

namespace ClearSkies.Infrastructure;

public sealed class InMemoryRunwayCatalog : IRunwayCatalog
{
    // Store runway headings per ICAO. Headings are magnetic and rounded to nearest 10.
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _db
        = new(StringComparer.OrdinalIgnoreCase)
        {
            // KSFO
            ["KSFO"] = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // 10/28 pair
                ["10L"] = 100,
                ["28R"] = 280,
                ["10R"] = 100,
                ["28L"] = 280,
                // 1/19 pair
                ["1L"] = 10,
                ["19R"] = 190,
                ["1R"] = 10,
                ["19L"] = 190,
            },

            // KSEA (example primary pair)
            ["KSEA"] = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["16L"] = 160,
                ["34R"] = 340,
                ["16C"] = 160,
                ["34C"] = 340,
                ["16R"] = 160,
                ["34L"] = 340,
            },

            // KDEN (typical)
            ["KDEN"] = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["16L"] = 160,
                ["34R"] = 340,
                ["16R"] = 160,
                ["34L"] = 340,
                ["17L"] = 170,
                ["35R"] = 350,
                ["17R"] = 170,
                ["35L"] = 350,
                ["08"] = 80,
                ["26"] = 260
            }
        };

    public int? GetHeadingDeg(string icao, string runway)
    {
        if (string.IsNullOrWhiteSpace(icao) || string.IsNullOrWhiteSpace(runway))
            return null;

        return _db.TryGetValue(icao.Trim().ToUpperInvariant(), out var runways)
            && runways.TryGetValue(runway.Trim().ToUpperInvariant(), out var hdg)
            ? hdg
            : (int?)null;
    }

    public IReadOnlyList<string> GetRunways(string icao)
        => _db.TryGetValue(icao.Trim().ToUpperInvariant(), out var rwy)
           ? rwy.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray()
           : Array.Empty<string>();

    public IReadOnlyList<(string Runway, int HeadingDeg)> GetRunwayHeadings(string icao)
        => _db.TryGetValue(icao.Trim().ToUpperInvariant(), out var rwy)
           ? rwy.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => (kv.Key, kv.Value)).ToArray()
           : Array.Empty<(string, int)>();
}

