using System;
using ClearSkies.App.Models;

namespace ClearSkies.App.Models
{
    // Mirrors ClearSkies.Api JSON response for /airports/{icao}/conditions
    public sealed class AirportConditionsDto
    {
        public string Icao { get; set; } = "";
        public int Category { get; set; }                 // API sends enum as number (e.g., 3 = VFR)
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
        public bool IsStale { get; set; }                  // NEW field from API
        public int AgeMinutes { get; set; }                // NEW field from API
    }
}


