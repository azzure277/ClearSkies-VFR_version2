namespace ClearSkies.Domain.Airports;

public sealed class Airport
{
    public required string Icao { get; init; }  // "KSFO"
    public string? Iata { get; init; }          // "SFO"
    public required string Name { get; init; }  // "San Francisco Intl"
    public string? City { get; init; }
    public string? State { get; init; }         // or Region
    public string? Country { get; init; }
}
