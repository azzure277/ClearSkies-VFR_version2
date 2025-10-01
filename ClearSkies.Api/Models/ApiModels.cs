namespace ClearSkies.Api.Models
{
    public class SearchRequest
    {
        public string? Q { get; set; }
        public int Take { get; set; } = 10;
        public int Page { get; set; } = 1;
    }

    public class SearchResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public class AirportSearchResult
    {
        public string Icao { get; set; } = string.Empty;
        public string? Iata { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
    }
}