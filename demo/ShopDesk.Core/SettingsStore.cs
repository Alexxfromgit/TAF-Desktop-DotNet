using System.Text.Json;

namespace ShopDesk;

/// <summary>
/// Settings persisted in the data directory (<c>settings.json</c>): the remembered user name. Tests reset them by
/// deleting the directory ([ResetAppData] in the framework).
/// </summary>
public sealed class SettingsStore(string dataDirectory)
{
    private string FilePath => Path.Combine(dataDirectory, "settings.json");

    public string? RememberedUsername
    {
        get
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                return doc.RootElement.TryGetProperty("rememberedUsername", out var value) ? value.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        set
        {
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new { rememberedUsername = value }));
        }
    }
}
