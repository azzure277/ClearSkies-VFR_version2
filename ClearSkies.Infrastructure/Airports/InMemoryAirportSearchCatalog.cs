using System;
using System.Collections.Generic;
using System.Linq;
using ClearSkies.Domain.Airports;

namespace ClearSkies.Infrastructure.Airports;

public sealed class InMemoryAirportSearchCatalog
{
    private static readonly List<Airport> _airports = new() {
        new() { Icao="KSFO", Iata="SFO", Name="San Francisco International", City="San Francisco", State="CA", Country="US" },
        new() { Icao="KLAX", Iata="LAX", Name="Los Angeles International",    City="Los Angeles",   State="CA", Country="US" },
        new() { Icao="KSEA", Iata="SEA", Name="Seattle–Tacoma International", City="Seattle",       State="WA", Country="US" },
        new() { Icao="KDEN", Iata="DEN", Name="Denver International",         City="Denver",        State="CO", Country="US" },
    };

    public IReadOnlyList<Airport> Search(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<Airport>();
        var q = query.Trim().ToUpperInvariant();

        static int Score(Airport a, string qUpper)
        {
            var nameUpper = a.Name.ToUpperInvariant();
            var iata = a.Iata?.ToUpperInvariant() ?? "";
            if (a.Icao.Equals(qUpper, StringComparison.Ordinal)) return 100;
            if (!string.IsNullOrEmpty(iata) && iata.Equals(qUpper, StringComparison.Ordinal)) return 90;
            if (nameUpper.StartsWith(qUpper)) return 80;
            if (nameUpper.Contains(qUpper)) return 60;
            if (!string.IsNullOrEmpty(iata) && iata.StartsWith(qUpper)) return 50;
            return 0;
        }

        return _airports
            .Select(a => (a, s: Score(a, q)))
            .Where(t => t.s > 0)
            .OrderByDescending(t => t.s)
            .ThenBy(t => t.a.Icao)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(t => t.a)
            .ToList();
    }
}