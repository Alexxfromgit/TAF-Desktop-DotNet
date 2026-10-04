using System.Globalization;
using System.Text;
using System.Text.Json;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Perf;

/// <summary>
/// The performance report: min/avg/p95/max per timing, compared with thresholds from <c>Perf:Thresholds</c>
/// (<c>"MainWindow ready": "2s"</c>). Written to <c>artifacts/taf-reports</c> and published in Allure.
/// </summary>
public static class PerfReport
{
    public sealed record Row(PerfStats Stats, TimeSpan? Threshold)
    {
        public bool Breached => Threshold is { } t && Stats.P95 > t.TotalMilliseconds;
    }

    public static IReadOnlyList<Row> Rows(IReadOnlyList<PerfStats> stats, IReadOnlyDictionary<string, string> thresholds) =>
        stats.Select(s => new Row(s, thresholds.TryGetValue(s.Label, out var t) ? TafConfig.ParseDuration(t) : null)).ToList();

    public static string Html(IReadOnlyList<Row> rows)
    {
        static string Ms(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Performance</title><style>")
            .Append("body{font-family:Segoe UI,sans-serif;margin:16px}table{border-collapse:collapse}")
            .Append("td,th{border:1px solid #ccc;padding:4px 10px;text-align:right}td:first-child,td:nth-child(2),th{text-align:left}")
            .Append(".breach{background:#fdd}</style></head><body><h2>Performance timings (ms)</h2>")
            .Append($"<p>{ReportFiles.Html(AllureResults.Timestamp())}</p><table><tr><th>Kind</th><th>Timing</th><th>Count</th>")
            .Append("<th>Min</th><th>Avg</th><th>p95</th><th>Max</th><th>Threshold</th></tr>");
        foreach (var row in rows)
        {
            var s = row.Stats;
            html.Append(row.Breached ? "<tr class=\"breach\">" : "<tr>")
                .Append($"<td>{s.Kind}</td><td>{ReportFiles.Html(s.Label)}</td><td>{s.Count}</td><td>{Ms(s.Min)}</td><td>{Ms(s.Avg)}</td>")
                .Append($"<td>{Ms(s.P95)}</td><td>{Ms(s.Max)}</td><td>{(row.Threshold is { } t ? Ms(t.TotalMilliseconds) : "")}</td></tr>");
        }
        return html.Append("</table></body></html>").ToString();
    }

    public static string Json(IReadOnlyList<Row> rows) => JsonSerializer.Serialize(rows.Select(r => new
    {
        kind = r.Stats.Kind.ToString(),
        timing = r.Stats.Label,
        count = r.Stats.Count,
        minMs = Math.Round(r.Stats.Min),
        avgMs = Math.Round(r.Stats.Avg),
        p95Ms = Math.Round(r.Stats.P95),
        maxMs = Math.Round(r.Stats.Max),
        thresholdMs = r.Threshold?.TotalMilliseconds,
        breached = r.Breached,
    }), new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Writes the report files and the Allure result (failed on a breach with <c>Perf:FailOnThreshold=true</c>).</summary>
    public static void Publish()
    {
        if (PerfCollector.IsEmpty)
        {
            return;
        }
        var config = TafConfig.Current;
        var rows = Rows(PerfCollector.Snapshot(), config.Section("Perf:Thresholds"));
        var html = Html(rows);
        var json = Json(rows);
        ReportFiles.Write("performance.html", html);
        ReportFiles.Write("performance.json", json);
        var breaches = rows.Where(r => r.Breached).Select(r => r.Stats.Label).ToList();
        var description = breaches.Count == 0
            ? $"{rows.Count} timings, no threshold exceeded."
            : $"p95 above threshold: {string.Join(", ", breaches)}";
        AllureResults.PublishReport("Performance timings", description, breaches.Count > 0 && config.Bool("Perf:FailOnThreshold", false),
            ("Performance timings", "text/html", ".html", html), ("performance.json", "application/json", ".json", json));
    }
}
