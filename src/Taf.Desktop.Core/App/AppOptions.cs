using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.App;

/// <summary>How a test starts (<c>App:Lifecycle</c>). Ordered from the most to the least thorough.</summary>
public enum StartMode
{
    /// <summary>Close the app, delete its data directory (settings, remembered user) and start it again.</summary>
    ResetAppData,

    /// <summary>Close the app and start it again: back to the start window, data kept.</summary>
    RestartApp,

    /// <summary>Keep the running app and continue where the previous test stopped (only for tests designed as a chain).</summary>
    Reuse,
}

/// <summary>The app under test, from the <c>App</c> configuration section.</summary>
public sealed record AppOptions
{
    /// <summary>Executable, absolute or relative to the repository root. Empty = attach to <see cref="ProcessName"/>.</summary>
    public string Path { get; init; } = "";

    public string Arguments { get; init; } = "";

    public string WorkingDirectory { get; init; } = "";

    /// <summary>Data directory deleted by <see cref="StartMode.ResetAppData"/> (empty = nothing to delete).</summary>
    public string DataDirectory { get; init; } = "";

    /// <summary>Process to attach to instead of starting one (an app started by someone else).</summary>
    public string ProcessName { get; init; } = "";

    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public StartMode Lifecycle { get; init; } = StartMode.RestartApp;

    public StartMode AfterFailure { get; init; } = StartMode.RestartApp;

    public bool ResetDataOnFirstStart { get; init; } = true;

    /// <summary>Extra environment variables of the app process (<c>App:Environment</c>).</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    public bool Attaches => string.IsNullOrEmpty(Path) && !string.IsNullOrEmpty(ProcessName);

    public static AppOptions FromConfig(TafConfig? config = null)
    {
        config ??= TafConfig.Current;
        var path = config.Find("App:Path");
        var dataDir = config.Find("App:DataDirectory");
        var workDir = config.Find("App:WorkingDirectory");
        return new AppOptions
        {
            Path = path is null ? "" : config.ResolvePath(path),
            Arguments = config.Get("App:Arguments", ""),
            WorkingDirectory = workDir is null ? "" : config.ResolvePath(workDir),
            DataDirectory = dataDir is null ? "" : config.ResolvePath(dataDir),
            ProcessName = config.Get("App:ProcessName", ""),
            StartTimeout = config.Duration("App:StartTimeout", TimeSpan.FromSeconds(30)),
            CloseTimeout = config.Duration("App:CloseTimeout", TimeSpan.FromSeconds(3)),
            Lifecycle = config.Enum("App:Lifecycle", StartMode.RestartApp),
            AfterFailure = config.Enum("App:AfterFailure", StartMode.RestartApp),
            ResetDataOnFirstStart = config.Bool("App:ResetDataOnFirstStart", true),
            Environment = config.Section("App:Environment"),
        };
    }
}
