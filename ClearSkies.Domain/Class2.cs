namespace ClearSkies.Domain;

public enum FlightCategory { LIFR, IFR, MVFR, VFR, Unknown }

public sealed record Metar(
    string Icao,
    DateTime Observed,
    decimal WindDirDeg,
    decimal WindKt,
    decimal? GustKt,
    decimal VisibilitySm,
    int? CeilingFtAgl,
    decimal TemperatureC,
    decimal DewpointC,
    decimal AltimeterInHg);
