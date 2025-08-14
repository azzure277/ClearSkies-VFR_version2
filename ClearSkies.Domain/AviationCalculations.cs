using System;

namespace ClearSkies.Domain;

public static class AviationCalculations
{
    /// <summary>
    /// Classifies flight category using standard VFR/MVFR/IFR/LIFR thresholds.
    /// </summary>
    public static FlightCategory ComputeCategory(int? ceilingFtAgl, decimal visibilitySm)
    {
        if ((ceilingFtAgl.HasValue && ceilingFtAgl < 500) || visibilitySm < 1) return FlightCategory.LIFR;
        if ((ceilingFtAgl.HasValue && ceilingFtAgl < 1000) || visibilitySm < 3) return FlightCategory.IFR;
        if ((ceilingFtAgl.HasValue && ceilingFtAgl <= 3000) || visibilitySm < 5) return FlightCategory.MVFR;
        if ((ceilingFtAgl.HasValue && ceilingFtAgl > 3000) && visibilitySm >= 5) return FlightCategory.VFR;
        return FlightCategory.Unknown;
    }

    /// <summary>
    /// Returns (headwind, crosswind) components for a given runway heading and wind.
    /// Crosswind is absolute value; headwind is signed (+ headwind, - tailwind).
    /// </summary>
    public static (decimal headwind, decimal crosswind) WindComponents(
        decimal runwayHeadingDeg, decimal windDirDeg, decimal windKt)
    {
        // Normalize to -180..+180 difference
        var angle = DegToRad(((windDirDeg - runwayHeadingDeg + 540) % 360) - 180);
        var head = windKt * (decimal)Math.Cos((double)angle);
        var cross = windKt * (decimal)Math.Sin((double)angle);
        return (Math.Round(head, 1), Math.Round(Math.Abs(cross), 1));
    }

    /// <summary>
    /// Simplified density altitude estimate.
    /// </summary>
    public static int DensityAltitudeFt(int fieldElevationFt, decimal tempC, decimal altimeterInHg)
    {
        // Pressure Altitude ≈ (29.92 - altimeter) * 1000 + field elevation
        var pressureAlt = (int)Math.Round(((29.92m - altimeterInHg) * 1000m) + fieldElevationFt);
        // ISA temperature at field elevation
        var isaTemp = 15m - 0.00198m * fieldElevationFt;
        var delta = tempC - isaTemp;
        // DA ≈ PressureAlt + 120 * (ΔT in °C)
        return pressureAlt + (int)Math.Round(120m * delta);
    }

    private static decimal DegToRad(decimal deg) => deg * (decimal)Math.PI / 180m;
}
