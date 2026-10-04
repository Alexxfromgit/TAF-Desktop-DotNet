using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Ui;

/// <summary>A standard Windows message box (<c>MessageBox.Show</c> in WPF and WinForms, Win32 <c>MessageBox</c>).</summary>
[Locate(ClassName = "#32770")]
public class MessageBoxDialog : Window
{
    [WindowIdentifier]
    [Locate(AutomationId = "65535", ControlType = ControlType.Text)]
    private UiElement message = null!;

    public string Message => message.Text;

    /// <summary>Clicks the button with this caption (<c>"Yes"</c>, <c>"Cancel"</c>, ...).</summary>
    public void Press(string button) => Element(button.ToLowerInvariant(), Locator.ByName(button, ControlType.Button)).Click();

    /// <summary>
    /// Confirms with the OK button, independent of the Windows language: OK of OK/Cancel has id 1, the only button of
    /// an OK-only message box has id 2.
    /// </summary>
    public void Ok() => Step.Run($"Confirm {Name} ('{Title}')", () =>
    {
        var ok = Element("ok", Locator.ById("1", ControlType.Button));
        (ok.IsPresent ? ok : Element("ok", Locator.ById("2", ControlType.Button))).Click();
        WaitClosed();
    });
}

/// <summary>The Windows open and save file dialogs (common item dialogs).</summary>
[Locate(ClassName = "#32770")]
public class FileDialog : Window
{
    [WindowIdentifier]
    [Locate(ClassName = "DUIViewWndClassName")]
    private UiElement explorerView = null!;

    /// <summary>The folder view of the dialog (identifies open/save dialogs among other #32770 windows).</summary>
    public UiElement ExplorerView => explorerView;

    /// <summary>Enters <paramref name="path"/> as the file name and confirms (Save or Open).</summary>
    public void Choose(string path) => Step.Run($"Choose '{path}' in {Name} ('{Title}')", () =>
    {
        var fileName = Element("fileName", Locator.ById("1001", ControlType.Edit));   // save dialog
        if (!Wait.Check(() => fileName.IsDisplayed))
        {
            fileName = Element("fileName", Locator.ById("1148", ControlType.Edit));    // open dialog
        }
        // Typed, not set: the dialog keeps its own file name and may ignore text set through UI Automation.
        fileName.TypeKeys(path);
        fileName.Press(VirtualKeyShort.ENTER);
        WaitClosed();
    });
}
