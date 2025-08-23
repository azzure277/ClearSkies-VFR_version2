using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ClearSkies.Domain;
using ClearSkies.Domain.Aviation;
using ClearSkies.Infrastructure.Weather;
using FluentAssertions;
using Xunit;

public class ApiBehaviorTests : IAsyncLifetime
{
    private HttpClient _client = default!;
    private bool _fail;

    // Weather provider that can be toggled to throw (simulating upstream failure)
    private IWeatherProvider MakeProvider()
        => new ToggleWeatherProvider(() => _fail);

    public async Task InitializeAsync()
    {
        // Default: CacheMinutes=1 for HIT behavior
        var app = new TestWebAppFactory(MakeProvider);
        _client = app.CreateClient();
        await Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(Timeout = 10000)]
    public async Task Valid_Icao_Miss_then_Hit()
    {
        _fail = false;
        var u = "/airports/KSFO/conditions?runway=28L";

        // First call => MISS
        var r1 = await _client.GetAsync(u);
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r1.Content.ReadFromJsonAsync<AirportConditionsDtoLike>())!.cacheResult.Should().Be("MISS");
        r1.Headers.TryGetValues("X-Cache", out var h1).Should().BeTrue();

        // Second call => HIT
        var r2 = await _client.GetAsync(u);
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r2.Content.ReadFromJsonAsync<AirportConditionsDtoLike>())!.cacheResult.Should().Be("HIT");
        r2.Headers.TryGetValues("X-Cache", out var h2).Should().BeTrue();
    }

    [Fact(Timeout = 10000)]
    public async Task Fallback_when_upstream_fails_and_cache_is_warm()
    {
    _fail = false;
    var u = "/airports/KSFO/conditions";

    // Use a factory with CacheMinutes=0 for fallback scenario
    var fallbackFactory = new TestWebAppFactory(MakeProvider, null, null, 0);
    var fallbackClient = fallbackFactory.CreateClient();

    // Warm the cache (MISS)
    var warm = await fallbackClient.GetAsync(u);
    warm.EnsureSuccessStatusCode();

    // Now fail upstream
    _fail = true;

    var r = await fallbackClient.GetAsync(u);
    r.StatusCode.Should().Be(HttpStatusCode.OK);

    var dto = await r.Content.ReadFromJsonAsync<AirportConditionsDtoLike>();
    dto!.cacheResult.Should().Be("FALLBACK");
    dto.isStale.Should().BeTrue();
    r.Headers.Contains("X-Cache-Fallback").Should().BeTrue();
    }

    [Fact(Timeout = 10000)]
    public async Task Unknown_Icao_returns_404()
    {
        _fail = false;
        var r = await _client.GetAsync("/airports/ZZZZ/conditions");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Timeout = 10000)]
    public async Task Invalid_Icao_returns_400()
    {
        _fail = false;
        var r = await _client.GetAsync("/airports/K3F*/conditions");
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Timeout = 10000)]
    public async Task Unknown_runway_returns_200_with_notice_header()
    {
        _fail = false;
        var r = await _client.GetAsync("/airports/KSFO/conditions?runway=99R");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Headers.Contains("X-Runway-Notice").Should().BeTrue();
    }

    // Minimal DTO projection for reading JSON
    private sealed class AirportConditionsDtoLike
    {
        public string? icao { get; set; }
        public bool isStale { get; set; }
        public string? cacheResult { get; set; }
    }
}

// Toggleable provider used by the integration tests
public sealed class ToggleWeatherProvider : IWeatherProvider
{
    private readonly Func<bool> _shouldFail;
    public ToggleWeatherProvider(Func<bool> shouldFail) => _shouldFail = shouldFail;

    public Task<Metar?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        if (_shouldFail()) throw new HttpRequestException("simulated failure");

        // minimal, plausible METAR (using correct record signature)
        var now = DateTime.UtcNow.AddMinutes(-5);
        return Task.FromResult<Metar?>(new Metar(
            icao,
            now,
            180,
            10,
            null,
            10,
            5000,
            29,
            17,
            30.05m
        ));
    }
}
