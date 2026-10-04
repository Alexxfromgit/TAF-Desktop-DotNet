using System.Collections.Concurrent;
using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.Perf;

public enum PerfKind
{
    AppStart,
    WindowReady,
    Transition,
    Lookup,
}

/// <summary>
/// Timings collected for free while functional tests run: app start, window ready and click-to-next-window
/// transitions (element lookups too with <c>Perf:Lookups=true</c>). Reported at the end of the run.
/// </summary>
public static class PerfCollector
{
    private static readonly ConcurrentDictionary<(PerfKind Kind, string Label), ConcurrentQueue<double>> Samples = new();

    public static void AppStart(string app, TimeSpan duration) => Record(PerfKind.AppStart, $"{app} start", duration);

    public static void WindowReady(string window, TimeSpan duration) => Record(PerfKind.WindowReady, $"{window} ready", duration);

    public static void Transition(string from, string action, string to, TimeSpan duration) =>
        Record(PerfKind.Transition, $"{from}.{action} -> {to}", duration);

    public static void Lookup(string element, TimeSpan duration)
    {
        if (Enabled && TafConfig.Current.Bool("Perf:Lookups", false))
        {
            Record(PerfKind.Lookup, $"find {element}", duration);
        }
    }

    public static void Record(PerfKind kind, string label, TimeSpan duration)
    {
        if (Enabled)
        {
            Samples.GetOrAdd((kind, label), _ => new ConcurrentQueue<double>()).Enqueue(duration.TotalMilliseconds);
        }
    }

    public static bool IsEmpty => Samples.IsEmpty;

    public static IReadOnlyList<PerfStats> Snapshot() =>
        Samples.Select(s => PerfStats.Of(s.Key.Kind, s.Key.Label, s.Value.ToArray()))
            .OrderBy(s => s.Kind).ThenBy(s => s.Label, StringComparer.Ordinal).ToList();

    public static void Clear() => Samples.Clear();

    private static bool Enabled
    {
        get
        {
            try
            {
                return TafConfig.Current.Bool("Perf:Enabled", true);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

/// <summary>Statistics of one timing in milliseconds.</summary>
public sealed record PerfStats(PerfKind Kind, string Label, int Count, double Min, double Avg, double P95, double Max)
{
    public static PerfStats Of(PerfKind kind, string label, IReadOnlyCollection<double> millis)
    {
        var sorted = millis.Order().ToArray();
        var p95 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
        return new PerfStats(kind, label, sorted.Length, sorted[0], sorted.Average(), p95, sorted[^1]);
    }
}
