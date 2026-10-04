namespace Taf.Desktop.Core.Ui;

/// <summary>How <see cref="UiElement.Click"/> clicks (<c>Ui:ClickMode</c>).</summary>
public enum ClickMode
{
    /// <summary>
    /// Invoke for WPF/XAML (no mouse needed), a real mouse click for WinForms and Win32. A WinForms Invoke that opens a
    /// modal dialog blocks UI Automation until the dialog closes, a mouse click does not.
    /// </summary>
    Auto,

    /// <summary>UI Automation Invoke pattern; returns early if the click opened a modal dialog.</summary>
    Invoke,

    /// <summary>Move the mouse and click, like a user. The window is brought to the front first.</summary>
    Mouse,
}
