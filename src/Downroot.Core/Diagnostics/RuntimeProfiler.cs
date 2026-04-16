using System.Diagnostics;

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
    private static Action<string> _logger = Console.WriteLine;
    private static int _frameWindow = 60;
    private static int _frameCount;
    private static long _frameTotalTicks;
    private static long _frameMaxTicks;
    private static long _frameStartTimestamp;

    public static bool Enabled { get; set; } = false;

    public static void Configure(Action<string>? logger = null, int frameWindow = 60)
    {
        lock (Sync)
        {
            if (logger is not null)
            {
                _logger = logger;
            }

            _frameWindow = Math.Max(1, frameWindow);
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

        _frameStartTimestamp = Stopwatch.GetTimestamp();
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
            if (_frameCount < _frameWindow)
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
        var frameAverageMs = ToMilliseconds(_frameTotalTicks / Math.Max(1, _frameCount));
        var frameMaxMs = ToMilliseconds(_frameMaxTicks);
        _logger($"[Profiler] Frames={_frameCount} frame_avg={frameAverageMs:F2}ms frame_max={frameMaxMs:F2}ms");

        foreach (var pair in SectionAggregates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var averageTicks = pair.Value.TotalTicks / Math.Max(1, pair.Value.Samples);
            _logger(
                $"[Profiler] {pair.Key} avg={ToMilliseconds(averageTicks):F2}ms max={ToMilliseconds(pair.Value.MaxTicks):F2}ms samples={pair.Value.Samples}");
        }

        foreach (var pair in CounterAggregates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            _logger($"[Profiler] {pair.Key} count={pair.Value.Total}");
        }

        _frameCount = 0;
        _frameTotalTicks = 0;
        _frameMaxTicks = 0;
        SectionAggregates.Clear();
        CounterAggregates.Clear();
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
}
