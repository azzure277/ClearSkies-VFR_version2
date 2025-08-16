namespace ClearSkies.App.Clean.Models;

public class AirportConditionsDto
{
    public string Icao { get; set; }
    public string Category { get; set; }
    public DateTime ObservedUtc { get; set; }
    public int WindDirDeg { get; set; }
    public int WindKt { get; set; }
    public int? GustKt { get; set; }
    public double VisibilitySm { get; set; }
    public int? CeilingFtAgl { get; set; }
    public double TemperatureC { get; set; }
    public double DewpointC { get; set; }
    public double AltimeterInHg { get; set; }
    public int HeadwindKt { get; set; }
    public int CrosswindKt { get; set; }
    public int DensityAltitudeFt { get; set; }
    public int AgeMinutes { get; set; }
    public bool IsStale { get; set; }
}
