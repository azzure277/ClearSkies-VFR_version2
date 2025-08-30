using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class ApiBehaviorTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    public ApiBehaviorTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Miss_then_Hit_for_same_icao()
    {
        var url = "/airports/KSFO/conditions?icao=KSFO";

        var r1 = await _client.GetAsync(url);
        r1.EnsureSuccessStatusCode();
        var present1 = r1.Headers.TryGetValues("X-Cache-Present", out var v1) ? v1.First() : "n/a";
        Assert.Equal("false", present1);

        var r2 = await _client.GetAsync(url);
        r2.EnsureSuccessStatusCode();
        var present2 = r2.Headers.TryGetValues("X-Cache-Present", out var v2) ? v2.First() : "n/a";
        Assert.Equal("true", present2);
    }
}
