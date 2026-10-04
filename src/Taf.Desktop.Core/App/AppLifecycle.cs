using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.App;

/// <summary>
/// Picks the <see cref="StartMode"/> of the next test. In order of precedence:
/// <list type="number">
/// <item>first test of the run - <see cref="StartMode.ResetAppData"/> (<c>App:ResetDataOnFirstStart</c>), no leftovers of earlier runs;</item>
/// <item>the app is not running (it crashed or was closed) - <see cref="StartMode.RestartApp"/>;</item>
/// <item>the previous test failed - <c>App:AfterFailure</c>, because a failure may leave the app in an unknown state;</item>
/// <item>[FreshApp] - <see cref="StartMode.RestartApp"/>;</item>
/// <item>[ResetAppData] or another [TestUser] than the previous test - <see cref="StartMode.ResetAppData"/>;</item>
/// <item>otherwise <c>App:Lifecycle</c> (default <see cref="StartMode.RestartApp"/>).</item>
/// </list>
/// [ResetAppData] and a user change always win over a weaker mode.
/// </summary>
public static class AppLifecyclePolicy
{
    public sealed record Situation(bool FirstStart, bool AppRunning, bool PreviousFailed, bool FreshApp, bool ResetAppData,
        string? PreviousUser, string? NextUser);

    public static StartMode Decide(Situation s, StartMode defaultMode, StartMode afterFailure, bool resetDataOnFirstStart)
    {
        var mode = s.FirstStart ? (resetDataOnFirstStart ? StartMode.ResetAppData : StartMode.RestartApp)
            : !s.AppRunning ? StartMode.RestartApp
            : s.PreviousFailed ? afterFailure
            : s.FreshApp ? StartMode.RestartApp
            : defaultMode;
        var userChanged = !s.FirstStart && !string.Equals(s.PreviousUser, s.NextUser, StringComparison.Ordinal);
        return s.ResetAppData || userChanged ? Stronger(StartMode.ResetAppData, mode) : mode;
    }

    private static StartMode Stronger(StartMode a, StartMode b) => a <= b ? a : b;
}

/// <summary>Applies the <see cref="AppLifecyclePolicy"/> before each test and remembers how the test ended.</summary>
public static class AppLifecycle
{
    private static bool started;
    private static bool previousFailed;
    private static string? previousUser;

    /// <summary>Starts, restarts or keeps the app for the test of <paramref name="scope"/>.</summary>
    public static StartMode Prepare(TestScope scope)
    {
        var options = AppOptions.FromConfig();
        var running = AppSession.Current is { HasExited: false };
        var mode = AppLifecyclePolicy.Decide(
            new AppLifecyclePolicy.Situation(!started, running, previousFailed, scope.Attribute<FreshAppAttribute>() != null,
                scope.Attribute<ResetAppDataAttribute>() != null, previousUser, scope.UserAlias),
            options.Lifecycle, options.AfterFailure, options.ResetDataOnFirstStart);
        if (options.Attaches)
        {
            mode = StartMode.Reuse;
        }
        Step.Run($"Prepare the app: {mode}", () =>
        {
            if (mode == StartMode.Reuse && running)
            {
                return;
            }
            AppSession.Current?.Close();
            if (mode == StartMode.ResetAppData)
            {
                AppSession.ResetAppData(options);
            }
            AppSession.Start(options);
        });
        started = true;
        previousUser = scope.UserAlias;
        return mode;
    }

    public static void Finished(bool failed) => previousFailed = failed;

    /// <summary>Closes the app at the end of the run.</summary>
    public static void Shutdown()
    {
        AppSession.Current?.Close();
        started = false;
        previousFailed = false;
        previousUser = null;
    }
}

/// <summary>Starts the app anew before this test, whatever <c>App:Lifecycle</c> says.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class FreshAppAttribute : Attribute;

/// <summary>Deletes the app's data directory and starts it anew before this test.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ResetAppDataAttribute : Attribute;
