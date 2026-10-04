using System.Runtime.InteropServices;
using Allure.Net.Commons;
using NUnit.Framework;
using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Perf;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Testing;

/// <summary>
/// Run-level set-up. Derive one class in the test assembly, outside any namespace or in the root namespace, and mark
/// it <c>[SetUpFixture]</c>: it prepares the Allure results (clean directory, failure categories) and at the end
/// closes the app, publishes the performance report and writes the report environment.
/// </summary>
public abstract class TafRunSetup
{
    [OneTimeSetUp]
    public void TafRunStarted()
    {
        DpiAwareness.Enable();
        if (TafConfig.Current.Bool("Taf:KeepAwake", true))
        {
            KeepAwake.Start();
        }
        if (TafConfig.Current.Bool("Report:CleanResults", true))
        {
            AllureLifecycle.Instance.CleanupResultDirectory();
        }
        AllureResults.InstallCategories();
        PerfCollector.Clear();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => AppSession.Current?.Kill();
        RunStarted();
    }

    [OneTimeTearDown]
    public void TafRunFinished()
    {
        try
        {
            RunFinished();
        }
        finally
        {
            AppLifecycle.Shutdown();
            PerfReport.Publish();
            AllureResults.WriteEnvironment(EnvironmentInfo());
            KeepAwake.Stop();
        }
    }

    /// <summary>Runs once before the first test (start mock backends, prepare data).</summary>
    protected virtual void RunStarted()
    {
    }

    /// <summary>Runs once after the last test, before the app is closed.</summary>
    protected virtual void RunFinished()
    {
    }

    /// <summary>Values shown on the report overview; extend to add your own.</summary>
    protected virtual IDictionary<string, string> EnvironmentInfo()
    {
        var config = TafConfig.Current;
        var info = new Dictionary<string, string>
        {
            ["Environment"] = config.Env,
            ["Executable"] = config.Find("App:Path") is { } path ? Path.GetFileName(path) : config.Get("App:ProcessName", ""),
            ["Lifecycle"] = config.Get("App:Lifecycle", ""),
            ["Click mode"] = config.Get("Ui:ClickMode", ""),
            ["OS"] = RuntimeInformation.OSDescription,
            [".NET"] = RuntimeInformation.FrameworkDescription,
        };
        foreach (var (key, value) in config.Section("Report:Parameters"))
        {
            info[key] = value;
        }
        if (Environment.GetEnvironmentVariable("GITHUB_RUN_ID") is { } run)
        {
            info["CI run"] = $"{Environment.GetEnvironmentVariable("GITHUB_SERVER_URL")}/{Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")}/actions/runs/{run}";
        }
        return info;
    }
}
