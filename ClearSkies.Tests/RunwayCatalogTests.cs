using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Runways;
using Xunit;

public class RunwayCatalogTests
{
    private readonly IRunwayCatalog _cat = new InMemoryRunwayCatalog();

    [Theory]
    [InlineData("KSFO","28L", 280, 288)]
    [InlineData("KDEN","17L", 170, 178)]
    public void Resolves_Heading(string icao, string rw, int min, int max)
    {
        Assert.True(_cat.TryGetMagneticHeading(icao, rw, out var hdg));
        Assert.InRange(hdg, min, max);
    }

    [Theory]
    [InlineData("KSFO","33C")]
    [InlineData("ZZZZ","28L")]
    public void Unknown_ReturnsFalse(string icao, string rw)
        => Assert.False(_cat.TryGetMagneticHeading(icao, rw, out _));
}
