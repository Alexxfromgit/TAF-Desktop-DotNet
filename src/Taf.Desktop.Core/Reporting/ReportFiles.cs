using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.Reporting;

/// <summary>Framework reports outside Allure: <c>artifacts/taf-reports/</c> (override with <c>Report:Directory</c>).</summary>
public static class ReportFiles
{
    public static string Directory
    {
        get
        {
            var config = TafConfig.Current;
            var dir = config.ResolvePath(config.Get("Report:Directory", "artifacts/taf-reports"));
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Write(string fileName, string content)
    {
        var path = Path.Combine(Directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Minimal HTML escaping for report pages.</summary>
    public static string Html(string text) => System.Net.WebUtility.HtmlEncode(text);
}
