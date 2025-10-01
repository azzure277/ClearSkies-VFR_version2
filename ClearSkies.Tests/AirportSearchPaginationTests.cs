using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using ClearSkies.Api.Models;

namespace ClearSkies.Tests;

public class AirportSearchPaginationTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public AirportSearchPaginationTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("a", 1, 5)] // First page, 5 items
    [InlineData("a", 2, 5)] // Second page, 5 items
    [InlineData("san", 1, 2)] // First page, 2 items
    public async Task Search_Returns_Paginated_Results(string query, int page, int take)
    {
        var resp = await _client.GetAsync($"/airports/search?q={query}&page={page}&take={take}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
                result!.Items.Count().Should().BeLessThanOrEqualTo(take);
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(take);
        result.Total.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0)] // Invalid: take cannot be 0
    [InlineData(51)] // Invalid: take cannot exceed 50
    [InlineData(-5)] // Invalid: take cannot be negative
    public async Task Search_Returns_BadRequest_For_Invalid_Take(int take)
    {
        var resp = await _client.GetAsync($"/airports/search?q=test&take={take}");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("Invalid request parameters");
        error.Details.Should().Contain("take must be between 1 and 50");
    }

    [Theory]
    [InlineData(0)] // Invalid: page cannot be 0
    [InlineData(-1)] // Invalid: page cannot be negative
    public async Task Search_Returns_BadRequest_For_Invalid_Page(int page)
    {
        var resp = await _client.GetAsync($"/airports/search?q=test&page={page}");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("Invalid request parameters");
        error.Details.Should().Contain("page must be greater than 0");
    }

    [Fact]
    public async Task Search_Returns_BadRequest_For_Missing_Query()
    {
        var resp = await _client.GetAsync("/airports/search");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("Invalid request parameters");
        error.Details.Should().Contain("Query parameter 'q' is required");
    }

    [Fact]
    public async Task Search_Returns_BadRequest_For_Empty_Query()
    {
        var resp = await _client.GetAsync("/airports/search?q=");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("Invalid request parameters");
        error.Details.Should().Contain("Query parameter 'q' is required");
    }

    [Fact]
    public async Task Search_Uses_Default_Pagination_Values()
    {
        var resp = await _client.GetAsync("/airports/search?q=san");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
        result!.Page.Should().Be(1); // Default page
        result.PageSize.Should().Be(10); // Default take
    }

    [Fact]
    public async Task Search_Response_Has_Correct_Structure()
    {
        var resp = await _client.GetAsync("/airports/search?q=sfo&take=1");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        
        var airport = result.Items.First();
        airport.Icao.Should().NotBeNullOrEmpty();
        airport.Name.Should().NotBeNullOrEmpty();
        // City, State, Country, Iata can be null but should be defined
        airport.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_Handles_Page_Beyond_Results()
    {
        // Search for something that should have limited results
        var resp = await _client.GetAsync("/airports/search?q=ksfo&page=100&take=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await resp.Content.ReadFromJsonAsync<SearchResponse<AirportSearchResult>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // No items on page 100
        result.Page.Should().Be(100);
        result.PageSize.Should().Be(10);
        result.Total.Should().BeGreaterThan(0); // But total should show actual count
    }
}