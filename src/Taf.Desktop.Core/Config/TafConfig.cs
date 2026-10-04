using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Config;

/// <summary>
/// Layered configuration, later layers win:
/// <list type="number">
/// <item><c>taf.defaults.json</c> embedded in the framework;</item>
/// <item><c>taf.json</c> next to the test assembly;</item>
/// <item><c>taf.{env}.json</c> - env from the test parameter <c>env</c> or <c>TAF_ENV</c> (default <c>default</c>);</item>
/// <item>environment variables <c>TAF__Section__Key</c>;</item>
/// <item>test run parameters (<c>.runsettings</c> or <c>dotnet test -- TestRunParameters.Parameter(name="App:Lifecycle", value="Reuse")</c>);</item>
/// <item><see cref="RuntimeOverrides"/> set during the run.</item>
/// </list>
/// Values can reference other keys: <c>"--api ${Mock:Url}"</c>. Built-in keys: <c>Taf:Root</c> (repository root) and
/// <c>Build:Configuration</c> (output folder of the test assembly: <c>debug</c> or <c>release</c>). Secrets are read with <see cref="Secret"/> from
/// environment variables only.
/// </summary>
public sealed partial class TafConfig
{
    public const string EnvVariablePrefix = "TAF__";

    private static readonly object Gate = new();
    private static TafConfig? current;

    private readonly IConfigurationRoot root;
    private readonly IReadOnlyDictionary<string, string?> environment;
    private string? rootDirectory;

    private TafConfig(IConfigurationRoot root, IReadOnlyDictionary<string, string?> environment, string baseDirectory, string env)
    {
        this.root = root;
        this.environment = environment;
        BaseDirectory = baseDirectory;
        Env = env;
    }

    /// <summary>The configuration of this test run (loaded on first use).</summary>
    public static TafConfig Current
    {
        get
        {
            lock (Gate)
            {
                return current ??= Load();
            }
        }
    }

    /// <summary>Selected environment, e.g. <c>winforms</c>.</summary>
    public string Env { get; }

    /// <summary>Directory the <c>taf*.json</c> files are read from (the test assembly directory).</summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Repository root, the base of relative paths such as <c>App:Path</c>: <c>Taf:RootDirectory</c>, or the first
    /// parent of <see cref="BaseDirectory"/> that contains a <c>*.slnx</c>/<c>*.sln</c> file or a <c>.git</c> folder.
    /// </summary>
    public string RootDirectory => rootDirectory ??= Find("Taf:RootDirectory") is { } configured
        ? Path.GetFullPath(configured, BaseDirectory)
        : DetectRoot(BaseDirectory);

    /// <summary>Replaces the configuration of the run (framework unit tests); null loads it again on next use.</summary>
    internal static void Use(TafConfig? config)
    {
        lock (Gate)
        {
            current = config;
        }
    }

    /// <summary>Loads the configuration again on next use (after changing files or environment variables).</summary>
    public static void Reload()
    {
        lock (Gate)
        {
            current = null;
        }
    }

    /// <summary>Loads a configuration explicitly; <see cref="Current"/> uses the defaults of all arguments.</summary>
    public static TafConfig Load(string? baseDirectory = null, string? env = null,
        IReadOnlyDictionary<string, string?>? environment = null, IReadOnlyDictionary<string, string?>? parameters = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        environment ??= ProcessEnvironment();
        parameters ??= TestRunParameters();
        env ??= Value(parameters, "env") ?? Value(environment, "TAF_ENV") ?? "default";

        var builder = new ConfigurationBuilder();
        var defaults = typeof(TafConfig).Assembly.GetManifestResourceStream("taf.defaults.json")
                       ?? throw new FrameworkException("taf.defaults.json is missing from Taf.Desktop.Core");
        builder.AddJsonStream(defaults);
        builder.AddJsonFile(Path.Combine(baseDirectory, "taf.json"), optional: true);
        builder.AddJsonFile(Path.Combine(baseDirectory, $"taf.{env}.json"), optional: true);
        builder.AddInMemoryCollection(environment
            .Where(e => e.Key.StartsWith(EnvVariablePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<string, string?>(e.Key[EnvVariablePrefix.Length..].Replace("__", ":"), e.Value)));
        builder.AddInMemoryCollection(parameters.Where(p => !p.Key.Equals("env", StringComparison.OrdinalIgnoreCase)));
        var config = new TafConfig(builder.Build(), environment, baseDirectory, env);

        if (config.Bool("Taf:SecretsGuard:Enabled", true))
        {
            var violations = SecretsGuard.Scan(baseDirectory);
            if (violations.Count > 0)
            {
                throw new FrameworkException("Secret values found in configuration files. Move them to environment variables "
                                             + $"({EnvVariablePrefix}Section__Key) and read them with TafConfig.Secret: "
                                             + string.Join(", ", violations));
            }
        }
        return config;
    }

    /// <summary>The value of <paramref name="key"/> with <c>${...}</c> references resolved, or null if unset or empty.</summary>
    public string? Find(string key)
    {
        var raw = Raw(key);
        return string.IsNullOrEmpty(raw) ? null : Resolve(raw, key, 0);
    }

    public string Get(string key) =>
        Find(key) ?? throw new FrameworkException(
            $"Missing configuration '{key}'. Set it in taf.json or the environment variable {EnvVariablePrefix}{key.Replace(":", "__")}");

    public string Get(string key, string fallback) => Find(key) ?? fallback;

    public bool Bool(string key, bool fallback) => Find(key) is { } v ? bool.Parse(v) : fallback;

    public int Int(string key, int fallback) => Find(key) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    public TimeSpan Duration(string key, TimeSpan fallback) => Find(key) is { } v ? ParseDuration(v) : fallback;

    public TimeSpan Duration(string key) => ParseDuration(Get(key));

    public T Enum<T>(string key, T fallback) where T : struct, Enum =>
        Find(key) is { } v
            ? System.Enum.TryParse<T>(v, ignoreCase: true, out var parsed)
                ? parsed
                : throw new FrameworkException($"'{key}' must be one of {string.Join(", ", System.Enum.GetNames<T>())}, found '{v}'")
            : fallback;

    /// <summary>All values under <paramref name="key"/>, keyed relative to it: <c>Section("App:Environment")</c>.</summary>
    public IReadOnlyDictionary<string, string> Section(string key)
    {
        var prefix = key + ":";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, _) in root.GetSection(key).AsEnumerable(makePathsRelative: true))
        {
            if (Find(prefix + k) is { } value)
            {
                values[k] = value;
            }
        }
        foreach (var (k, v) in RuntimeOverrides.All.Where(o => o.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            values[k[prefix.Length..]] = v;
        }
        return values;
    }

    /// <summary>
    /// A secret, read only from the environment variable <c>TAF__{key with ':' replaced by '__'}</c>
    /// (CI secrets, <c>.runsettings</c> environment variables, or your shell). Never from files.
    /// </summary>
    public string Secret(string key) =>
        FindSecret(key) ?? throw new EnvironmentException(
            $"Secret '{key}' is not set. Set the environment variable {SecretVariable(key)}.");

    public string? FindSecret(string key) => Value(environment, SecretVariable(key));

    public static string SecretVariable(string key) => EnvVariablePrefix + key.Replace(":", "__");

    /// <summary>An absolute path for <paramref name="path"/>, relative paths resolved against <see cref="RootDirectory"/>.</summary>
    public string ResolvePath(string path) => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path), RootDirectory);

    /// <summary>Parses <c>500ms</c>, <c>10s</c>, <c>2m</c>, <c>1h</c> or a .NET time span such as <c>00:00:10</c>.</summary>
    public static TimeSpan ParseDuration(string text)
    {
        var match = DurationPattern().Match(text.Trim());
        if (match.Success)
        {
            var amount = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return match.Groups[2].Value switch
            {
                "ms" => TimeSpan.FromMilliseconds(amount),
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                _ => TimeSpan.FromHours(amount),
            };
        }
        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span)
            ? span
            : throw new FrameworkException($"'{text}' is not a duration (examples: 500ms, 10s, 2m, 00:00:10)");
    }

    /// <summary><c>1.5s</c>, <c>250ms</c>: the short form used in messages and reports.</summary>
    public static string Format(TimeSpan duration) =>
        duration.TotalSeconds >= 1
            ? duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s"
            : duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + "ms";

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*(ms|s|m|h)$")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex Reference();

    private string Resolve(string value, string key, int depth)
    {
        if (depth > 10)
        {
            throw new FrameworkException($"'{key}' has circular ${{...}} references");
        }
        return Reference().Replace(value, m =>
        {
            var referenced = m.Groups[1].Value;
            var raw = Raw(referenced);
            if (string.IsNullOrEmpty(raw))
            {
                throw new FrameworkException($"'{key}' references ${{{referenced}}}, which is not set");
            }
            return Resolve(raw, referenced, depth + 1);
        });
    }

    private string? Raw(string key) =>
        RuntimeOverrides.TryGet(key, out var overridden) ? overridden
        : key.Equals("Taf:Root", StringComparison.OrdinalIgnoreCase) ? RootDirectory.Replace('\\', '/')
        : key.Equals("Build:Configuration", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(BaseDirectory.TrimEnd('\\', '/'))
        : root[key];

    private static string DetectRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any() || Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
        }
        return start;
    }

    private static string? Value(IReadOnlyDictionary<string, string?> values, string key) =>
        values.FirstOrDefault(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } value ? value : null;

    private static Dictionary<string, string?> ProcessEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            values[(string)entry.Key] = entry.Value as string;
        }
        return values;
    }

    private static Dictionary<string, string?> TestRunParameters()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var parameters = global::NUnit.Framework.TestContext.Parameters;
        foreach (var name in parameters.Names)
        {
            values[name] = parameters.Get(name);
        }
        return values;
    }

    /// <summary>The configuration of the test assembly that is running: <c>Debug</c>, <c>Release</c>.</summary>
    internal static string BuildConfiguration(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
}
