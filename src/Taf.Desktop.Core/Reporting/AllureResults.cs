using System.Globalization;
using System.Text;
using System.Text.Json;
using Allure.Net.Commons;
using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.Reporting;

/// <summary>Files the framework writes into the Allure results directory besides the test results.</summary>
public static class AllureResults
{
    public static string Directory => AllureLifecycle.Instance.ResultsDirectory;

    /// <summary>Copies the framework's failure categories (<c>categories.json</c>) into the results.</summary>
    public static void InstallCategories()
    {
        using var stream = typeof(AllureResults).Assembly.GetManifestResourceStream("taf.categories.json")!;
        System.IO.Directory.CreateDirectory(Directory);
        using var file = File.Create(Path.Combine(Directory, "categories.json"));
        stream.CopyTo(file);
    }

    /// <summary>Writes <c>environment.properties</c> (shown on the report overview).</summary>
    public static void WriteEnvironment(IEnumerable<KeyValuePair<string, string>> values)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var lines = values.Where(v => !string.IsNullOrEmpty(v.Value)).Select(v => $"{v.Key}={v.Value.Replace("\\", "\\\\")}");
        File.WriteAllLines(Path.Combine(Directory, "environment.properties"), lines);
    }

    /// <summary>
    /// Publishes a report produced at the end of the run (e.g. performance timings) as a result of its own in the
    /// suite "Framework reports", with the given attachments and the run's <c>Report:Parameters</c> / <c>Report:Labels</c>
    /// (so reports of several app flavours stay apart in a merged report).
    /// </summary>
    public static void PublishReport(string name, string description, bool failed, params (string Name, string MimeType, string Extension, string Content)[] attachments)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var config = TafConfig.Current;
        var parameters = config.Section("Report:Parameters").OrderBy(p => p.Key).ToList();
        var labels = new List<object> { new { name = "suite", value = "Framework reports" }, new { name = "framework", value = "taf-desktop" } };
        labels.AddRange(config.Section("Report:Labels").Where(l => l.Key != "suite").Select(l => new { name = l.Key, value = l.Value }));
        var uuid = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var files = new List<object>();
        foreach (var attachment in attachments)
        {
            var source = $"{Guid.NewGuid():N}-attachment{attachment.Extension}";
            File.WriteAllText(Path.Combine(Directory, source), attachment.Content, Encoding.UTF8);
            files.Add(new { name = attachment.Name, source, type = attachment.MimeType });
        }
        var result = new
        {
            uuid,
            historyId = "taf-report-" + name.ToLowerInvariant().Replace(' ', '-') + string.Concat(parameters.Select(p => $"-{p.Value}")),
            name,
            fullName = "Framework reports." + name,
            description,
            status = failed ? "failed" : "passed",
            statusDetails = failed ? new { message = description } : null,
            stage = "finished",
            start = now,
            stop = now,
            labels,
            parameters = parameters.Select(p => new { name = p.Key, value = p.Value }),
            attachments = files,
        };
        File.WriteAllText(Path.Combine(Directory, $"{uuid}-result.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }

    internal static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
