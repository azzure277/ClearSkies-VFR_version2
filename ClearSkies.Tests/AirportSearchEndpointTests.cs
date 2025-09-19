using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ClearSkies.Tests;

public class AirportSearchEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public AirportSearchEndpointTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("SFO", "KSFO")]
    [InlineData("lax", "KLAX")]
    [InlineData("sea", "KSEA")]
    [InlineData("den", "KDEN")]
    [InlineData("San Francisco", "KSFO")]
    public async Task Search_Returns_Expected_Airport(string query, string expectedIcao)
    {
        var resp = await _client.GetAsync($"/airports/search?q={query}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var results = doc.RootElement.EnumerateArray().ToList();
        results.Should().NotBeNull();
        results.Should().Contain(r => r.GetProperty("icao").GetString() == expectedIcao);
    }

    [Fact]
    public async Task Search_Returns_BadRequest_On_Empty_Query()
    {
        var resp = await _client.GetAsync("/airports/search");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_Respects_Limit()
    {
        var resp = await _client.GetAsync("/airports/search?q=a&limit=2");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var results = doc.RootElement.EnumerateArray().ToList();
        results.Should().NotBeNull();
    results.Count.Should().BeLessThanOrEqualTo(2);
    }
}
