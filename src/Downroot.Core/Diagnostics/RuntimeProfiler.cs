using System.Diagnostics;
using System.Text.Json;

namespace Downroot.Core.Diagnostics;

public static class RuntimeProfiler
{
    private sealed class SectionAggregate
    {
        public long TotalTicks;
        public long MaxTicks;
        public int Samples;
    }

    private sealed class CounterAggregate
    {
        public long Total;
    }

    public readonly ref struct Scope
    {
        private readonly string _name;
        private readonly long _startTimestamp;

        internal Scope(string name)
        {
            _name = name;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            RuntimeProfiler.Record(_name, Stopwatch.GetTimestamp() - _startTimestamp);
        }
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, SectionAggregate> SectionAggregates = [];
    private static readonly Dictionary<string, CounterAggregate> CounterAggregates = [];
    private static string? _logFilePath;
    private static TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
    private static int _frameCount;
    private static long _frameTotalTicks;
    private static long _frameMaxTicks;
    private static long _frameStartTimestamp;
    private static long _windowStartTimestamp;

    public static bool Enabled { get; set; } = false;

    public static void Configure(string? logFilePath = null, TimeSpan? flushInterval = null)
    {
        lock (Sync)
        {
            _logFilePath = string.IsNullOrWhiteSpace(logFilePath) ? null : logFilePath;
            _flushInterval = flushInterval.GetValueOrDefault(TimeSpan.FromSeconds(5));
            if (_flushInterval <= TimeSpan.Zero)
            {
                _flushInterval = TimeSpan.FromSeconds(5);
            }
        }
    }

    public static Scope Measure(string name)
    {
        return Enabled ? new Scope(name) : default;
    }

    public static void Increment(string name, long amount = 1)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (!CounterAggregates.TryGetValue(name, out var aggregate))
            {
                aggregate = new CounterAggregate();
                CounterAggregates[name] = aggregate;
            }

            aggregate.Total += amount;
        }
    }

    public static void BeginFrame()
    {
        if (!Enabled)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        lock (Sync)
        {
            if (_windowStartTimestamp == 0)
            {
                _windowStartTimestamp = timestamp;
            }

            _frameStartTimestamp = timestamp;
        }
    }

    public static void EndFrame()
    {
        if (!Enabled)
        {
            return;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - _frameStartTimestamp;
        lock (Sync)
        {
            _frameCount++;
            _frameTotalTicks += elapsedTicks;
            _frameMaxTicks = Math.Max(_frameMaxTicks, elapsedTicks);
            var windowElapsed = Stopwatch.GetElapsedTime(_windowStartTimestamp, Stopwatch.GetTimestamp());
            if (windowElapsed < _flushInterval)
            {
                return;
            }

            Flush();
        }
    }

    public static void FlushNow()
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (_frameCount <= 0 && SectionAggregates.Count == 0 && CounterAggregates.Count == 0)
            {
                return;
            }

            Flush();
        }
    }

    private static void Record(string name, long elapsedTicks)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Sync)
        {
            if (!SectionAggregates.TryGetValue(name, out var aggregate))
            {
                aggregate = new SectionAggregate();
                SectionAggregates[name] = aggregate;
            }

            aggregate.TotalTicks += elapsedTicks;
            aggregate.MaxTicks = Math.Max(aggregate.MaxTicks, elapsedTicks);
            aggregate.Samples++;
        }
    }

    private static void Flush()
    {
        var now = DateTimeOffset.UtcNow;
        var frameAverageMs = _frameCount > 0 ? ToMilliseconds(_frameTotalTicks / Math.Max(1, _frameCount)) : 0d;
        var frameMaxMs = ToMilliseconds(_frameMaxTicks);
        var payload = new ProfilerSnapshot(
            now,
            _frameCount,
            frameAverageMs,
            frameMaxMs,
            SectionAggregates
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair =>
                    {
                        var averageTicks = pair.Value.TotalTicks / Math.Max(1, pair.Value.Samples);
                        return new SectionSnapshot(
                            ToMilliseconds(averageTicks),
                            ToMilliseconds(pair.Value.MaxTicks),
                            pair.Value.Samples);
                    },
                    StringComparer.Ordinal),
            CounterAggregates
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Total,
                    StringComparer.Ordinal));

        AppendSnapshot(payload);

        _frameCount = 0;
        _frameTotalTicks = 0;
        _frameMaxTicks = 0;
        _windowStartTimestamp = Stopwatch.GetTimestamp();
        SectionAggregates.Clear();
        CounterAggregates.Clear();
    }

    private static void AppendSnapshot(ProfilerSnapshot payload)
    {
        if (string.IsNullOrWhiteSpace(_logFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(_logFilePath, JsonSerializer.Serialize(payload) + Environment.NewLine);
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    private sealed record ProfilerSnapshot(
        DateTimeOffset TimestampUtc,
        int Frames,
        double FrameAverageMs,
        double FrameMaxMs,
        IReadOnlyDictionary<string, SectionSnapshot> Sections,
        IReadOnlyDictionary<string, long> Counters);

    private sealed record SectionSnapshot(
        double AverageMs,
        double MaxMs,
        int Samples);
}
