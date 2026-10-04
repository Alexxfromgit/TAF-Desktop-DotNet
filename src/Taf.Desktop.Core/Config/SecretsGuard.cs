using System.Text.Json;
using System.Text.RegularExpressions;

namespace Taf.Desktop.Core.Config;

/// <summary>
/// Rejects secret values in configuration files: a key that looks like a secret (password, token, secret, api key,
/// credential) must not have a value in <c>taf*.json</c>. Secrets come from environment variables only
/// (<see cref="TafConfig.Secret"/>).
/// </summary>
public static partial class SecretsGuard
{
    [GeneratedRegex("(password|passwd|secret|token|api-?key|apikey|credential)", RegexOptions.IgnoreCase)]
    private static partial Regex SecretKey();

    [GeneratedRegex(@"^taf(\.[A-Za-z0-9_-]+)?\.json$")]
    private static partial Regex ConfigFile();

    /// <summary>The configuration files of a directory: <c>taf.json</c> and <c>taf.{env}.json</c>.</summary>
    public static IEnumerable<string> ConfigFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.json").Where(f => ConfigFile().IsMatch(Path.GetFileName(f))).OrderBy(f => f, StringComparer.Ordinal);

    public static bool LooksLikeSecret(string key) => SecretKey().IsMatch(key);

    /// <summary>Violations in all <c>taf*.json</c> files of <paramref name="directory"/>, e.g. <c>taf.json: Users:admin:Password</c>.</summary>
    public static IReadOnlyList<string> Scan(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }
        var violations = new List<string>();
        foreach (var file in ConfigFiles(directory))
        {
            violations.AddRange(ScanJson(File.ReadAllText(file)).Select(key => $"{Path.GetFileName(file)}: {key}"));
        }
        return violations;
    }

    /// <summary>Secret-looking keys with a non-empty value in a JSON document.</summary>
    public static IReadOnlyList<string> ScanJson(string json)
    {
        var found = new List<string>();
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        Walk(doc.RootElement, "", found);
        return found;
    }

    private static void Walk(JsonElement element, string path, List<string> found)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (var property in element.EnumerateObject())
        {
            var key = path.Length == 0 ? property.Name : path + ":" + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Walk(property.Value, key, found);
            }
            else if (LooksLikeSecret(property.Name) && property.Value.ValueKind == JsonValueKind.String
                     && !string.IsNullOrEmpty(property.Value.GetString()))
            {
                found.Add(key);
            }
        }
    }
}
