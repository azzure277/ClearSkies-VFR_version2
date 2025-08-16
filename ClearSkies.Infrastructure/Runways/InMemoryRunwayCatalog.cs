using System;
using System.Collections.Generic;
using System.Linq;
using ClearSkies.Domain.Aviation;

namespace ClearSkies.Infrastructure.Runways
{
    /// <summary>
    /// In-memory runway catalog for development/testing.
    /// Resolves ICAO + runway designator (e.g., "KSFO" + "28L") to magnetic heading degrees.
    /// </summary>
    public sealed class InMemoryRunwayCatalog : ClearSkies.Domain.Aviation.IRunwayCatalog
    {
        private readonly Dictionary<string, AirportRunways> _airports;

        public InMemoryRunwayCatalog()
        {
            // NOTE: Headings are reasonable examples for now.
            // We can swap in an authoritative dataset later.
            _airports = new(StringComparer.OrdinalIgnoreCase)
            {
                ["KSFO"] = new AirportRunways("KSFO", new[]
                {
                    new RunwayInfo("01L",  1, RunwaySide.Left,   014),
                    new RunwayInfo("01R",  1, RunwaySide.Right,  014),
                    new RunwayInfo("19L", 19, RunwaySide.Left,   194),
                    new RunwayInfo("19R", 19, RunwaySide.Right,  194),
                    new RunwayInfo("28L", 28, RunwaySide.Left,   284),
                    new RunwayInfo("28R", 28, RunwaySide.Right,  284),
                    new RunwayInfo("10L", 10, RunwaySide.Left,   104),
                    new RunwayInfo("10R", 10, RunwaySide.Right,  104),
                }),
                ["KDEN"] = new AirportRunways("KDEN", new[]
                {
                    new RunwayInfo("16L", 16, RunwaySide.Left,   164),
                    new RunwayInfo("16R", 16, RunwaySide.Right,  164),
                    new RunwayInfo("17L", 17, RunwaySide.Left,   174),
                    new RunwayInfo("17R", 17, RunwaySide.Right,  174),
                    new RunwayInfo("34L", 34, RunwaySide.Left,   344),
                    new RunwayInfo("34R", 34, RunwaySide.Right,  344),
                    new RunwayInfo("35L", 35, RunwaySide.Left,   354),
                    new RunwayInfo("35R", 35, RunwaySide.Right,  354),
                    new RunwayInfo("07",   7, RunwaySide.None,    074),
                    new RunwayInfo("25",  25, RunwaySide.None,    254),
                })
            };
        }

        // Implements ClearSkies.Domain.Aviation.IRunwayCatalog
        public AirportRunways? GetAirportRunways(string icao)
            => _airports.TryGetValue(icao, out var arpt) ? arpt : null;

        public bool TryGetMagneticHeading(string icao, string runwayDesignator, out int magneticHeadingDeg)
        {
            magneticHeadingDeg = 0;

            if (!_airports.TryGetValue(icao, out var arpt))
                return false;

            if (!RunwayDesignatorParser.TryParse(runwayDesignator, out var number, out var side))
                return false;

            var match = arpt.Runways.FirstOrDefault(r =>
                r.Number == number &&
                (side == RunwaySide.None || r.Side == side));

            if (match is null) return false;

            magneticHeadingDeg = match.MagneticHeadingDeg;
            return true;
        }
    }
}
