using System;

namespace ClearSkies.Domain;

public sealed record AirportConditionsDto(
    string Icao,
    FlightCategory Category,
    DateTime ObservedUtc,
    decimal WindDirDeg,
    decimal WindKt,
    decimal? GustKt,
    decimal VisibilitySm,
    int? CeilingFtAgl,
    decimal TemperatureC,
    decimal DewpointC,
    decimal AltimeterInHg,
    decimal HeadwindKt,
    decimal CrosswindKt,
    int DensityAltitudeFt);
