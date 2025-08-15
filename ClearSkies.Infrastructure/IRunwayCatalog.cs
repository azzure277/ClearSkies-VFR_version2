namespace ClearSkies.Infrastructure;

public interface IRunwayCatalog
{
    int? GetHeadingDeg(string icao, string runway);

    // NEW: list available runways for an ICAO
    IReadOnlyList<string> GetRunways(string icao);

    // NEW: list with headings
    IReadOnlyList<(string Runway, int HeadingDeg)> GetRunwayHeadings(string icao);
}
