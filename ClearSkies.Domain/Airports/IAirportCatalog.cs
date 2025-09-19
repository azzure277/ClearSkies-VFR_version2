namespace ClearSkies.Domain.Airports;

public interface IAirportCatalog
{
    IReadOnlyList<Airport> Search(string query, int limit = 10);
}
