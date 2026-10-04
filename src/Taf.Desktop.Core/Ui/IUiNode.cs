using System.Drawing;
using FlaUI.Core.Definitions;

namespace Taf.Desktop.Core.Ui;

/// <summary>
/// One node of the UI Automation tree. The framework works against this interface, so the window model can be unit
/// tested with fake trees; <see cref="FlaUiNode"/> implements it over FlaUI (UIA3).
/// </summary>
public interface IUiNode
{
    string Name { get; }

    string AutomationId { get; }

    string ClassName { get; }

    ControlType ControlType { get; }

    /// <summary>UI framework of the element: <c>WPF</c>, <c>WinForm</c>, <c>Win32</c>, <c>XAML</c>, ...</summary>
    string FrameworkId { get; }

    int ProcessId { get; }

    bool IsOffscreen { get; }

    bool IsEnabled { get; }

    bool IsPassword { get; }

    bool IsReadOnly { get; }

    Rectangle Bounds { get; }

    /// <summary>What a user reads: the value of edits and combo boxes, otherwise the name.</summary>
    string Text { get; }

    /// <summary>Value pattern value, or null if the element has none.</summary>
    string? Value { get; }

    bool? IsToggled { get; }

    bool? IsSelected { get; }

    bool? IsExpanded { get; }

    bool SupportsInvoke { get; }

    bool SupportsValue { get; }

    bool SupportsSelect { get; }

    bool CanExpand { get; }

    IReadOnlyList<IUiNode> FindAll(Locator locator, bool childrenOnly = false);

    void Invoke();

    void MouseClick();

    void DoubleClick();

    void RightClick();

    void Focus();

    void SetValue(string value);

    /// <summary>Focuses the element, selects its content and replaces it by typing <paramref name="text"/>.</summary>
    void TypeText(string text);

    void PressKey(FlaUI.Core.WindowsAPI.VirtualKeyShort key);

    void Toggle();

    void Select();

    void Expand();

    void Collapse();

    void ScrollIntoView();

    void CloseWindow();

    byte[] CapturePng();
}
