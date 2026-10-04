using System.Collections.Concurrent;

namespace Taf.Desktop.Core.Config;

/// <summary>
/// Configuration values set while the suite runs (highest precedence), e.g. the URL of a mock backend that only
/// exists after it started on a free port. Read through <see cref="TafConfig"/> like any other key.
/// </summary>
public static class RuntimeOverrides
{
    private static readonly ConcurrentDictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);

    public static void Set(string key, string value) => Values[key] = value;

    public static void Remove(string key) => Values.TryRemove(key, out _);

    public static void Clear() => Values.Clear();

    internal static bool TryGet(string key, out string value) => Values.TryGetValue(key, out value!);

    internal static IEnumerable<KeyValuePair<string, string>> All => Values;
}
