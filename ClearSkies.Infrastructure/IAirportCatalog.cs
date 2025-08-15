namespace ClearSkies.Infrastructure;

public interface IAirportCatalog
{
    /// Returns field elevation (feet MSL) for the ICAO, or null if unknown.
    int? GetElevationFt(string icao);
}
