using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using ClearSkies.Api.Models;

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
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
        result!.Items.Should().Contain(r => r.Icao == expectedIcao);
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
        var resp = await _client.GetAsync("/airports/search?q=a&take=2");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
        result!.Items.Count().Should().BeLessThanOrEqualTo(2);
    }
}