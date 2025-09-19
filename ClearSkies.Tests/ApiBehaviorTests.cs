using ClearSkies.Infrastructure;

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

namespace ClearSkies.Tests
{
    public sealed class AirportConditionsDtoLike
    {
        public string? icao { get; set; }
        public bool isStale { get; set; }
        public string? cacheResult { get; set; }
    }

    [CollectionDefinition("WebApp collection")]
    public class WebAppCollection : ICollectionFixture<WebAppFixture> { }

    public class WebAppFixture : IDisposable
    {
        public TestWebAppFactory Factory { get; } = new();
        public void Dispose() => Factory.Dispose();
    }

    [Collection("WebApp collection")]
    public class ApiBehaviorTests : IAsyncLifetime
    {
        private readonly TestWebAppFactory _appFactory;
        private readonly HttpClient _client;

        public ApiBehaviorTests(WebAppFixture fixture)
        {
            _appFactory = fixture.Factory;
            _client = _appFactory.CreateClient();
        }

        public Task InitializeAsync()
        {
            // No cache to clear while caching is paused
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ...existing test methods and TestCacheDebug...
        public static class TestCacheDebug
        {
            public static void PrintCacheState()
            {
                var type = typeof(StaticTestMetarCache);
                var dictField = type.GetField("_dict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (dictField != null)
                {
                    var dict = dictField.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                    {
                        foreach (System.Collections.DictionaryEntry entry in dict)
                        {
                            var key = entry.Key;
                            var value = entry.Value;
                            if (value != null)
                            {
                                var metarProp = value.GetType().GetProperty("Metar");
                                var insertedProp = value.GetType().GetProperty("Inserted");
                                var ttlProp = value.GetType().GetProperty("Ttl");
                                var metar = metarProp?.GetValue(value);
                                var observedProp = metar?.GetType().GetProperty("Observed");
                                var observed = observedProp?.GetValue(metar);
                                var inserted = insertedProp?.GetValue(value);
                                var ttl = ttlProp?.GetValue(value);
                                Console.WriteLine($"  [CACHE] key={key} inserted={inserted} ttl={ttl} metar.Observed={observed}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  [CACHE] dict is null");
                    }
                }
                else
                {
                    Console.WriteLine("  [CACHE] _dict field not found");
                }
            }
        }
    }

        public static class TestCacheDebug
        {
            public static void PrintCacheState()
            {
                var type = typeof(StaticTestMetarCache);
                var dictField = type.GetField("_dict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (dictField != null)
                {
                    var dict = dictField.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                    {
                        foreach (System.Collections.DictionaryEntry entry in dict)
                        {
                            var key = entry.Key;
                            var value = entry.Value;
                            if (value != null)
                            {
                                var metarProp = value.GetType().GetProperty("Metar");
                                var insertedProp = value.GetType().GetProperty("Inserted");
                                var ttlProp = value.GetType().GetProperty("Ttl");
                                var metar = metarProp?.GetValue(value);
                                var observedProp = metar?.GetType().GetProperty("Observed");
                                var observed = observedProp?.GetValue(metar);
                                var inserted = insertedProp?.GetValue(value);
                                var ttl = ttlProp?.GetValue(value);
                                Console.WriteLine($"  [CACHE] key={key} inserted={inserted} ttl={ttl} metar.Observed={observed}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  [CACHE] dict is null");
                    }
                }
                else
                {
                    Console.WriteLine("  [CACHE] _dict field not found");
                }
            }
        }
    }








