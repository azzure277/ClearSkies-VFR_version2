using Microsoft.AspNetCore.Mvc;
namespace ClearSkies.Api.Problems
{
    public static class ProblemFactory
    {
        public static ProblemDetails NoMetar(string icao) =>
            new ProblemDetails
            {
                Title = "No METAR available",
                Detail = $"Upstream provider returned no observation for station '{icao}'.",
                Status = StatusCodes.Status502BadGateway,
                Type = "https://clear-skies.dev/problems/no-metar"
            };
    }
}
