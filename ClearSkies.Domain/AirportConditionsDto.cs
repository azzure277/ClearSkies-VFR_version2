using System;

namespace ClearSkies.Domain
{
    public sealed class AirportConditionsDto
    {
    public string Icao { get; set; } = "";
    public string RawMetar { get; set; } = "";
        public int Category { get; set; }
        public DateTime ObservedUtc { get; set; }
        public decimal WindDirDeg { get; set; }
        public decimal WindKt { get; set; }
        public decimal? GustKt { get; set; }
        public decimal VisibilitySm { get; set; }
        public int? CeilingFtAgl { get; set; }
        public decimal TemperatureC { get; set; }
        public decimal DewpointC { get; set; }
        public decimal AltimeterInHg { get; set; }
        public decimal HeadwindKt { get; set; }
        public decimal CrosswindKt { get; set; }
        public int DensityAltitudeFt { get; set; }
        public bool IsStale { get; set; }
        public string? CacheResult { get; set; }
        public int AgeMinutes { get; set; }
    }
}


