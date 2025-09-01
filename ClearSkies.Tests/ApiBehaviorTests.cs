using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ClearSkies.Domain;
// If TestWebAppFactory is in the same namespace, no using needed. Otherwise:
using ClearSkies.Tests;

public class ApiBehaviorTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;
    public ApiBehaviorTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void ClearCacheAndReseed(Metar metar)
    {
        // Clear IMemoryCache
        var cache = _factory.Services.GetService<IMemoryCache>();
        if (cache is MemoryCache mc)
        {
            mc.Compact(1.0); // Remove all entries
        }
        // Reseed provider
        _factory.TestProvider.NextMetar = metar;
    }

    [Fact]
    public async Task Returns_304_when_IfNoneMatch_matches()
    {
        // Clear cache and seed provider for deterministic ETag
        ClearCacheAndReseed(new Metar(
            icao: "KSFO",
            observed: DateTime.Parse("2025-08-27T12:00:00Z"),
            rawMetar: "KSFO 271200Z 29018KT 10SM FEW010 SCT025 17/12 A2992"
        ));

        var url = "/airports/KSFO/conditions?icao=KSFO";

        var r1 = await _client.GetAsync(url);
        r1.EnsureSuccessStatusCode();
           // Diagnostic: print all headers
           foreach (var h in r1.Headers)
               System.Diagnostics.Debug.WriteLine($"[DIAG] r1 header: {h.Key} = {string.Join(",", h.Value)}");
           foreach (var h in r1.Content.Headers)
               System.Diagnostics.Debug.WriteLine($"[DIAG] r1 content header: {h.Key} = {string.Join(",", h.Value)}");
    var etagHeader = r1.Headers.ETag;
    Assert.NotNull(etagHeader); // Ensure ETag is present
    var etag = etagHeader.Tag;

    var req = new HttpRequestMessage(HttpMethod.Get, url);
    req.Headers.TryAddWithoutValidation("If-None-Match", etag);
    var r2 = await _client.SendAsync(req);

    Assert.Equal(System.Net.HttpStatusCode.NotModified, r2.StatusCode);
    Assert.NotNull(r2.Headers.ETag); // Ensure ETag is present in 304 response
    Assert.Equal(etag, r2.Headers.ETag.Tag);
    }

    [Fact]
    public async Task Miss_then_Hit_for_same_icao()
    {
        var url = "/airports/KSFO/conditions?icao=KSFO";

        // Clear cache and seed provider for deterministic cache behavior
        ClearCacheAndReseed(new Metar(
            icao: "KSFO",
            observed: DateTime.Parse("2025-08-27T12:00:00Z"),
            rawMetar: "KSFO 271200Z 29018KT 10SM FEW010 SCT025 17/12 A2992"
        ));

    // Diagnostic: print cache state and provider's NextMetar before first request
    var cache = _factory.Services.GetService<IMemoryCache>() as MemoryCache;
    System.Diagnostics.Debug.WriteLine($"[DIAG] Cache instance before r1: {cache?.GetType().Name} @ {cache?.GetHashCode()}");
    System.Diagnostics.Debug.WriteLine($"[DIAG] Provider NextMetar before r1: {_factory.TestProvider.NextMetar?.RawMetar}");

    var r1 = await _client.GetAsync(url);
    System.Diagnostics.Debug.WriteLine($"[DIAG] Provider NextMetar after r1: {_factory.TestProvider.NextMetar?.RawMetar}");
        r1.EnsureSuccessStatusCode();
        // Diagnostic: print all headers
        foreach (var h in r1.Headers)
            System.Diagnostics.Debug.WriteLine($"[DIAG] r1 header: {h.Key} = {string.Join(",", h.Value)}");
        foreach (var h in r1.Content.Headers)
            System.Diagnostics.Debug.WriteLine($"[DIAG] r1 content header: {h.Key} = {string.Join(",", h.Value)}");
        // Diagnostic: print cache state after first request
    System.Diagnostics.Debug.WriteLine($"[DIAG] Cache instance after r1: {cache?.GetType().Name} @ {cache?.GetHashCode()}");
        var h1 = r1.Headers.TryGetValues("X-Cache-Present", out var missVals) ? missVals.SingleOrDefault() : null;
        if (h1 == null)
        {
            h1 = r1.Content.Headers.TryGetValues("X-Cache-Present", out var missVals2) ? missVals2.SingleOrDefault() : null;
        }
        var dto1 = await r1.Content.ReadFromJsonAsync<ClearSkies.Domain.AirportConditionsDto>();
        System.Diagnostics.Debug.WriteLine($"[DIAG] r1 RawMetar: {dto1?.RawMetar}");
        if (h1 == null && dto1 != null && dto1.RawMetar != null && dto1.RawMetar.Contains("X-Cache-Present:"))
        {
            h1 = dto1.RawMetar.Split('|').FirstOrDefault(x => x.StartsWith("X-Cache-Present:"))?.Split(':').Last();
        }
        System.Diagnostics.Debug.WriteLine($"[DIAG] r1 X-Cache-Present: {h1}");
        Assert.Equal("false", h1); // MISS

    var r2 = await _client.GetAsync(url);
    System.Diagnostics.Debug.WriteLine($"[DIAG] Provider NextMetar after r2: {_factory.TestProvider.NextMetar?.RawMetar}");
        r2.EnsureSuccessStatusCode();
        // Diagnostic: print all headers
        foreach (var h in r2.Headers)
            System.Diagnostics.Debug.WriteLine($"[DIAG] r2 header: {h.Key} = {string.Join(",", h.Value)}");
        foreach (var h in r2.Content.Headers)
            System.Diagnostics.Debug.WriteLine($"[DIAG] r2 content header: {h.Key} = {string.Join(",", h.Value)}");
        // Diagnostic: print cache state after second request
    System.Diagnostics.Debug.WriteLine($"[DIAG] Cache instance after r2: {cache?.GetType().Name} @ {cache?.GetHashCode()}");
        var h2 = r2.Headers.TryGetValues("X-Cache-Present", out var hitVals) ? hitVals.SingleOrDefault() : null;
        if (h2 == null)
        {
            h2 = r2.Content.Headers.TryGetValues("X-Cache-Present", out var hitVals2) ? hitVals2.SingleOrDefault() : null;
        }
        var dto2 = await r2.Content.ReadFromJsonAsync<ClearSkies.Domain.AirportConditionsDto>();
        System.Diagnostics.Debug.WriteLine($"[DIAG] r2 RawMetar: {dto2?.RawMetar}");
        if (h2 == null && dto2 != null && dto2.RawMetar != null && dto2.RawMetar.Contains("X-Cache-Present:"))
        {
            h2 = dto2.RawMetar.Split('|').FirstOrDefault(x => x.StartsWith("X-Cache-Present:"))?.Split(':').Last();
        }
        System.Diagnostics.Debug.WriteLine($"[DIAG] r2 X-Cache-Present: {h2}");
        Assert.Equal("true", h2); // HIT
    }
}
