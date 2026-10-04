using System.Runtime.InteropServices;

namespace Taf.Desktop.Core.App;

/// <summary>
/// Keeps the display on and the PC awake while UI tests run (<c>Taf:KeepAwake</c>, default on). A display that turns
/// off or a session that locks blocks all mouse and keyboard input to the app under test.
/// </summary>
public static class KeepAwake
{
    private const uint Continuous = 0x80000000;
    private const uint SystemRequired = 0x00000001;
    private const uint DisplayRequired = 0x00000002;

    public static void Start() => SetThreadExecutionState(Continuous | SystemRequired | DisplayRequired);

    public static void Stop() => SetThreadExecutionState(Continuous);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
