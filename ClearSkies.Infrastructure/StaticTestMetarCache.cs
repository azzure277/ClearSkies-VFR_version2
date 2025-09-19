using System;
using System.Collections.Concurrent;
using ClearSkies.Domain;

namespace ClearSkies.Infrastructure
{
    public class StaticTestMetarCache : IMetarCache
    {
        private class Entry
        {
            public Metar Metar { get; set; }
            public DateTime Inserted { get; set; }
            public TimeSpan Ttl { get; set; }
        }
        private static readonly ConcurrentDictionary<string, Entry> _dict = new();
        private readonly Func<DateTime> _clock;
        public StaticTestMetarCache(Func<DateTime> clock)
        {
            _clock = clock;
        }
        public bool TryGet(string icao, out Metar? metar, DateTime now)
        {
            var key = GetKey(icao);
            if (_dict.TryGetValue(key, out var entry))
            {
                var age = (now - entry.Inserted);
                Console.WriteLine($"[StaticTestMetarCache] TryGet key={key} now={now:o} inserted={entry.Inserted:o} ttl={entry.Ttl} age={age} metar.Observed={entry.Metar.Observed:o}");
                if (age <= entry.Ttl)
                {
                    // Return a new Metar instance to avoid stale CacheResult
                    metar = new Metar(
                        entry.Metar.Icao,
                        entry.Metar.Observed,
                        entry.Metar.WindDirDeg,
                        entry.Metar.WindKt,
                        entry.Metar.GustKt,
                        entry.Metar.VisibilitySm,
                        entry.Metar.CeilingFtAgl,
                        entry.Metar.TemperatureC,
                        entry.Metar.DewpointC,
                        entry.Metar.AltimeterInHg
                    )
                    {
                        RawMetar = entry.Metar.RawMetar
                    };
                    return true;
                }
            }
            metar = null;
            return false;
        }

        public bool TryGetStale(string icao, out Metar? metar)
        {
            var key = GetKey(icao);
            if (_dict.TryGetValue(key, out var entry))
            {
                Console.WriteLine($"[StaticTestMetarCache] TryGetStale key={key} inserted={entry.Inserted:o} ttl={entry.Ttl}");
                // Return a new Metar instance to avoid stale CacheResult
                metar = new Metar(
                    entry.Metar.Icao,
                    entry.Metar.Observed,
                    entry.Metar.WindDirDeg,
                    entry.Metar.WindKt,
                    entry.Metar.GustKt,
                    entry.Metar.VisibilitySm,
                    entry.Metar.CeilingFtAgl,
                    entry.Metar.TemperatureC,
                    entry.Metar.DewpointC,
                    entry.Metar.AltimeterInHg
                )
                {
                    RawMetar = entry.Metar.RawMetar
                };
                return true;
            }
            metar = null;
            return false;
        }

        public void Set(string icao, Metar metar, DateTime now, TimeSpan ttl)
        {
            var key = GetKey(icao);
            Console.WriteLine($"[StaticTestMetarCache] Set key={key} now={now:o} ttl={ttl} metar.Observed={metar.Observed:o}");
            _dict[key] = new Entry { Metar = metar, Inserted = now, Ttl = ttl };
        }
        public void Invalidate(string icao)
        {
            _dict.TryRemove(GetKey(icao), out _);
        }
        public static void ClearAll() => _dict.Clear();
        private static string GetKey(string icao) => $"metar:{icao.ToUpperInvariant()}";
    }
}
