using System.Collections.Generic;

namespace ClearSkies.Domain.Aviation
{
    public enum RunwaySide { None = 0, Left, Right, Center }

    public sealed record RunwayInfo(
        string Designator,
        int Number,
        RunwaySide Side,
        int MagneticHeadingDeg
    );

    public sealed record AirportRunways(
        string Icao,
        IReadOnlyList<RunwayInfo> Runways
    );
}
