using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Ui;

/// <summary>
/// Where windows come from: by default the top-level windows and popups of the running app. Unit tests replace
/// <see cref="TopLevel"/> with a fake tree.
/// </summary>
public static class UiRuntime
{
    public static Func<IReadOnlyList<IUiNode>> TopLevel { get; set; } = AppTopLevel;

    public static void Reset() => TopLevel = AppTopLevel;

    /// <summary>All windows of the app: top-level ones and the dialogs they own (owned windows are their children).</summary>
    internal static IReadOnlyList<IUiNode> Windows()
    {
        var windows = new List<IUiNode>();
        foreach (var top in TopLevel().Where(t => t.ControlType == FlaUI.Core.Definitions.ControlType.Window))
        {
            AddWithOwned(top, windows, 0);
        }
        return windows;
    }

    /// <summary>Owned windows are child windows of their owner (a message box of a dialog of the main window).</summary>
    private static void AddWithOwned(IUiNode window, List<IUiNode> windows, int depth)
    {
        windows.Add(window);
        if (depth < 5)
        {
            foreach (var owned in window.FindAll(Locator.Of(FlaUI.Core.Definitions.ControlType.Window), childrenOnly: true))
            {
                AddWithOwned(owned, windows, depth + 1);
            }
        }
    }

    /// <summary>Popup menus and drop-down lists that some frameworks (WinForms) show as top-level windows.</summary>
    internal static IReadOnlyList<IUiNode> Popups() =>
        TopLevel().Where(t => t.ControlType is FlaUI.Core.Definitions.ControlType.Menu or FlaUI.Core.Definitions.ControlType.List).ToList();

    private static IReadOnlyList<IUiNode> AppTopLevel() =>
        AppSession.Current?.TopLevelNodes()
        ?? throw new FrameworkException("No app is running. Derive the test from DesktopTest or call AppSession.Start first.");
}
