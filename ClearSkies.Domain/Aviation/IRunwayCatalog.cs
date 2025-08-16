namespace ClearSkies.Domain.Aviation
{
    public interface IRunwayCatalog
    {
        /// <summary>
        /// Resolve magnetic heading (degrees) for a runway at an airport.
        /// Example: ICAO "KSFO", runway "28L" -> 284 deg (depending on dataset).
        /// </summary>
        /// <param name="icao">Airport ICAO (e.g., KSFO)</param>
        /// <param name="runwayDesignator">Runway "XX[LR/C]" (e.g., 28L, 17R, 07)</param>
        /// <param name="magneticHeadingDeg">Output magnetic heading in degrees</param>
        /// <returns>true if found; otherwise false</returns>
        bool TryGetMagneticHeading(string icao, string runwayDesignator, out int magneticHeadingDeg);

        /// <summary>
        /// Get all known runways for an airport, or null if airport unknown.
        /// </summary>
        AirportRunways? GetAirportRunways(string icao);
    }
}
