using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Perf;
using Taf.Desktop.Core.Reporting;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.App;

/// <summary>The running app under test: its process and the UI Automation connection to it.</summary>
public sealed class AppSession : IDisposable
{
    private AppSession(Application application, UIA3Automation automation, AppOptions options)
    {
        Application = application;
        Automation = automation;
        Options = options;
    }

    /// <summary>The app of this test run, or null if none is running.</summary>
    public static AppSession? Current { get; private set; }

    public Application Application { get; }

    public UIA3Automation Automation { get; }

    public AppOptions Options { get; }

    public int ProcessId => Application.ProcessId;

    /// <summary>Executable name, e.g. <c>ShopDesk.Wpf</c>.</summary>
    public string Name => Options.Attaches ? Options.ProcessName : System.IO.Path.GetFileNameWithoutExtension(Options.Path);

    public bool HasExited
    {
        get
        {
            try
            {
                return Application.HasExited;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>Starts the app (or attaches to it) and waits for its first window.</summary>
    public static AppSession Start(AppOptions? options = null)
    {
        options ??= AppOptions.FromConfig();
        DpiAwareness.Enable();
        Current?.Close();
        var name = options.Attaches ? options.ProcessName : System.IO.Path.GetFileNameWithoutExtension(options.Path);
        return Step.Run($"{(options.Attaches ? "Attach to" : "Start")} {name}", () =>
        {
            var watch = Stopwatch.StartNew();
            var application = options.Attaches ? Attach(options) : Launch(options);
            var session = new AppSession(application, new UIA3Automation(), options);
            if (!Wait.Until(() => session.HasExited || session.TopLevelNodes().Any(n => n.ControlType == ControlType.Window && !n.IsOffscreen),
                    options.StartTimeout) || session.HasExited)
            {
                var reason = session.HasExited
                    ? $"{name} exited during start (exit code {SafeExitCode(application)})"
                    : $"{name} showed no window within {TafConfig.Format(options.StartTimeout)}";
                session.Kill();
                throw new EnvironmentException(reason);
            }
            PerfCollector.AppStart(name, watch.Elapsed);
            Current = session;
            return session;
        });
    }

    /// <summary>Top-level windows and popups of the app process.</summary>
    public IReadOnlyList<IUiNode> TopLevelNodes()
    {
        if (HasExited)
        {
            return [];
        }
        var desktop = Automation.GetDesktop();
        return desktop.FindAllChildren(desktop.ConditionFactory.ByProcessId(ProcessId)).Select(e => (IUiNode)new FlaUiNode(e)).ToList();
    }

    /// <summary>
    /// Closes the app: asks its main window to close and kills it if it does not exit within <c>App:CloseTimeout</c>.
    /// With a dialog still open (a test that stopped halfway) it is killed right away - it could not close anyway.
    /// </summary>
    public void Close()
    {
        try
        {
            if (!HasExited && !Options.Attaches)
            {
                if (HasOpenDialog() || !Process.GetProcessById(ProcessId).CloseMainWindow() || !Wait.Until(() => HasExited, Options.CloseTimeout))
                {
                    Application.Kill();
                }
            }
        }
        catch (Exception)
        {
            Kill();
        }
        finally
        {
            Release();
        }
    }

    private bool HasOpenDialog() =>
        Wait.Check(() => TopLevelNodes().Count(n => n.ControlType == ControlType.Window) > 1
                         || TopLevelNodes().Any(n => n.FindAll(Locator.Of(ControlType.Window), childrenOnly: true).Count > 0));

    public void Kill()
    {
        try
        {
            if (!HasExited && !Options.Attaches)
            {
                Application.Kill();
            }
        }
        catch (Exception)
        {
            // already gone
        }
        Release();
    }

    public void Dispose() => Close();

    /// <summary>Deletes the app's data directory (<c>App:DataDirectory</c>), retrying while files are still locked.</summary>
    public static void ResetAppData(AppOptions options)
    {
        if (string.IsNullOrEmpty(options.DataDirectory) || !Directory.Exists(options.DataDirectory))
        {
            return;
        }
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(options.DataDirectory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(200);
            }
        }
    }

    private static Application Launch(AppOptions options)
    {
        if (!File.Exists(options.Path))
        {
            throw new EnvironmentException($"The app is not at {options.Path}. Build the solution first (dotnet build) or set App:Path.");
        }
        var start = new ProcessStartInfo(options.Path, options.Arguments)
        {
            WorkingDirectory = options.WorkingDirectory.Length > 0 ? options.WorkingDirectory : System.IO.Path.GetDirectoryName(options.Path)!,
            UseShellExecute = false,
        };
        foreach (var (key, value) in options.Environment)
        {
            start.Environment[key] = value;
        }
        return Application.Launch(start);
    }

    private static Application Attach(AppOptions options)
    {
        try
        {
            return Application.Attach(options.ProcessName);
        }
        catch (Exception e)
        {
            throw new EnvironmentException($"No running process '{options.ProcessName}' to attach to", e);
        }
    }

    private static string SafeExitCode(Application application)
    {
        try
        {
            return application.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private void Release()
    {
        Automation.Dispose();
        Application.Dispose();
        if (Current == this)
        {
            Current = null;
        }
    }
}
