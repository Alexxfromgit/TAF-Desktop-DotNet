using System.Runtime.InteropServices;

namespace Taf.Desktop.Core.App;

/// <summary>
/// Makes the test process per-monitor DPI aware. Without it, Windows scales the coordinates a DPI-unaware test
/// process sees (125%, 150%, 175% displays): element bounds, mouse positions and screenshots then disagree with the
/// real screen. Called before the first UI Automation call.
/// </summary>
public static class DpiAwareness
{
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);
    private static bool done;

    public static void Enable()
    {
        if (done)
        {
            return;
        }
        done = true;
        try
        {
            SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }
        catch (EntryPointNotFoundException)
        {
            // Windows older than 10 1703: keep the default
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
