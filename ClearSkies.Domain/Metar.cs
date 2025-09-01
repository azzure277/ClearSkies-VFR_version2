using System;

namespace ClearSkies.Domain
{
    public sealed class Metar
    {
        public string Icao { get; set; }
        public DateTime Observed { get; set; }
        public decimal WindDirDeg { get; set; }
        public decimal WindKt { get; set; }
        public decimal? GustKt { get; set; }
        public decimal VisibilitySm { get; set; }
        public int? CeilingFtAgl { get; set; }
        public decimal TemperatureC { get; set; }
        public decimal DewpointC { get; set; }
        public decimal AltimeterInHg { get; set; }
        public string RawMetar { get; set; }

        public Metar(
            string icao,
            DateTime observed,
            decimal windDirDeg,
            decimal windKt,
            decimal? gustKt,
            decimal visibilitySm,
            int? ceilingFtAgl,
            decimal temperatureC,
            decimal dewpointC,
            decimal altimeterInHg)
        {
            Icao = icao;
            Observed = observed;
            WindDirDeg = windDirDeg;
            WindKt = windKt;
            GustKt = gustKt;
            VisibilitySm = visibilitySm;
            CeilingFtAgl = ceilingFtAgl;
            TemperatureC = temperatureC;
            DewpointC = dewpointC;
            AltimeterInHg = altimeterInHg;
            RawMetar = "";
        }

        // Optionally, add a constructor for RawMetar if needed elsewhere
        public Metar(string icao, DateTime observed, string rawMetar)
        {
            Icao = icao;
            Observed = observed;
            RawMetar = rawMetar;
        }
    }
}
