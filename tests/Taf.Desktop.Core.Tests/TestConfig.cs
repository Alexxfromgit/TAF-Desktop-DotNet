using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Tests;

/// <summary>Loads the unit-test configuration (fast waits) and resets global state between tests.</summary>
internal static class TestConfig
{
    public static TafConfig Load(IReadOnlyDictionary<string, string?>? environment = null, IReadOnlyDictionary<string, string?>? parameters = null,
        string? env = null) =>
        TafConfig.Load(AppContext.BaseDirectory, env, environment ?? new Dictionary<string, string?>(), parameters ?? new Dictionary<string, string?>());

    public static void UseDefaults()
    {
        RuntimeOverrides.Clear();
        TafConfig.Use(Load());
        UiRuntime.Reset();
    }
}
