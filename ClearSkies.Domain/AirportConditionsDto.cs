using System;

namespace ClearSkies.Domain;

public record AirportConditionsDto(
    string Icao,
    int Category,
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
    int DensityAltitudeFt,
    bool IsStale, // true if the conditions are older than 1 hour
    int AgeMinutes // minutes since observation
);


