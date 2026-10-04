using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Ui;

/// <summary><see cref="IUiNode"/> over a FlaUI element. Property reads never throw for unsupported properties.</summary>
public sealed class FlaUiNode(AutomationElement element) : IUiNode
{
    public AutomationElement Element { get; } = element;

    public string Name => Element.Properties.Name.ValueOrDefault ?? "";

    public string AutomationId => Element.Properties.AutomationId.ValueOrDefault ?? "";

    public string ClassName => Element.Properties.ClassName.ValueOrDefault ?? "";

    public ControlType ControlType => Element.Properties.ControlType.ValueOrDefault;

    public string FrameworkId => Element.Properties.FrameworkId.ValueOrDefault ?? "";

    public int ProcessId => Element.Properties.ProcessId.ValueOrDefault;

    public bool IsOffscreen => Element.Properties.IsOffscreen.ValueOrDefault;

    public bool IsEnabled => Element.Properties.IsEnabled.ValueOrDefault;

    public bool IsPassword => Element.Properties.IsPassword.ValueOrDefault;

    public bool IsReadOnly => Element.Patterns.Value.TryGetPattern(out var value) && value.IsReadOnly.ValueOrDefault;

    public Rectangle Bounds => Element.Properties.BoundingRectangle.ValueOrDefault;

    public string Text => ControlType switch
    {
        ControlType.Edit or ControlType.Document => Value ?? Name,
        ControlType.ComboBox => Value ?? SelectedItemName() ?? "",
        _ => Name,
    };

    public string? Value => Element.Patterns.Value.TryGetPattern(out var value) ? value.Value.ValueOrDefault : null;

    public bool? IsToggled => Element.Patterns.Toggle.TryGetPattern(out var toggle) ? toggle.ToggleState.ValueOrDefault == ToggleState.On : null;

    public bool? IsSelected => Element.Patterns.SelectionItem.TryGetPattern(out var item) ? item.IsSelected.ValueOrDefault : null;

    public bool? IsExpanded => Element.Patterns.ExpandCollapse.TryGetPattern(out var expand)
        ? expand.ExpandCollapseState.ValueOrDefault is ExpandCollapseState.Expanded or ExpandCollapseState.PartiallyExpanded
        : null;

    public bool SupportsInvoke => Element.Patterns.Invoke.IsSupported;

    public bool SupportsValue => Element.Patterns.Value.IsSupported;

    public bool SupportsSelect => Element.Patterns.SelectionItem.IsSupported;

    public bool CanExpand => Element.Patterns.ExpandCollapse.IsSupported;

    public IReadOnlyList<IUiNode> FindAll(Locator locator, bool childrenOnly = false)
    {
        if (locator.Kind == LocatorKind.XPath)
        {
            return Element.FindAllByXPath(locator.Value).Select(e => (IUiNode)new FlaUiNode(e)).ToList();
        }
        var cf = Element.ConditionFactory;
        ConditionBase condition = locator.Kind switch
        {
            LocatorKind.AutomationId => cf.ByAutomationId(locator.Value),
            LocatorKind.Name => cf.ByName(locator.Value),
            LocatorKind.ClassName => cf.ByClassName(locator.Value),
            _ => TrueCondition.Default,
        };
        if (locator.ControlType is { } controlType)
        {
            condition = condition is TrueCondition ? cf.ByControlType(controlType) : condition.And(cf.ByControlType(controlType));
        }
        var found = childrenOnly ? Element.FindAllChildren(condition) : Element.FindAllDescendants(condition);
        return found.Select(e => (IUiNode)new FlaUiNode(e)).Where(locator.Matches).ToList();
    }

    public void Invoke()
    {
        if (!Element.Patterns.Invoke.TryGetPattern(out var invoke))
        {
            throw new FrameworkException($"{Describe()} cannot be invoked (no Invoke pattern); use a mouse click");
        }
        invoke.Invoke();
    }

    public void MouseClick()
    {
        EnsureClickable();
        Element.Click(moveMouse: false);
    }

    public void DoubleClick()
    {
        EnsureClickable();
        Element.DoubleClick(moveMouse: false);
    }

    public void RightClick()
    {
        EnsureClickable();
        Element.RightClick(moveMouse: false);
    }

    public void Focus() => Element.Focus();

    public void SetValue(string value)
    {
        if (!Element.Patterns.Value.TryGetPattern(out var pattern))
        {
            throw new FrameworkException($"{Describe()} has no Value pattern");
        }
        pattern.SetValue(value);
    }

    public void TypeText(string text)
    {
        FocusForKeyboard();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(VirtualKeyShort.DELETE);
        if (text.Length > 0)
        {
            Keyboard.Type(text);
        }
    }

    public void PressKey(VirtualKeyShort key)
    {
        FocusForKeyboard();
        Keyboard.Type(key);
    }

    public void Toggle() => Pattern(Element.Patterns.Toggle.PatternOrDefault, "Toggle").Toggle();

    public void Select() => Pattern(Element.Patterns.SelectionItem.PatternOrDefault, "SelectionItem").Select();

    public void Expand() => Pattern(Element.Patterns.ExpandCollapse.PatternOrDefault, "ExpandCollapse").Expand();

    public void Collapse() => Pattern(Element.Patterns.ExpandCollapse.PatternOrDefault, "ExpandCollapse").Collapse();

    public void ScrollIntoView()
    {
        if (Element.Patterns.ScrollItem.TryGetPattern(out var scroll))
        {
            scroll.ScrollIntoView();
        }
    }

    public void CloseWindow() => Pattern(Element.Patterns.Window.PatternOrDefault, "Window").Close();

    public byte[] CapturePng()
    {
        var file = Path.Combine(Path.GetTempPath(), $"taf-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = Capture.Element(Element))
            {
                image.ToFile(file);
            }
            return File.ReadAllBytes(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public override string ToString() => Describe();

    private string Describe() => $"{ControlType} '{Name}' (AutomationId '{AutomationId}')";

    private string? SelectedItemName() =>
        Element.Patterns.Selection.TryGetPattern(out var selection) && selection.Selection.ValueOrDefault is { Length: > 0 } items
            ? items[0].Properties.Name.ValueOrDefault
            : null;

    private T Pattern<T>(T? pattern, string name) where T : class =>
        pattern ?? throw new FrameworkException($"{Describe()} does not support the {name} pattern");

    /// <summary>
    /// Keyboard input goes to the focused element of the foreground window: bring the window to the front and wait
    /// until the element really has the keyboard focus (a window that just opened may still be activating).
    /// </summary>
    private void FocusForKeyboard()
    {
        BringToFront();
        Element.Focus();
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!Element.Properties.HasKeyboardFocus.ValueOrDefault && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
            Element.Focus();
        }
        if (!Element.Properties.HasKeyboardFocus.ValueOrDefault)
        {
            throw new EnvironmentException($"{Describe()} did not get the keyboard focus: another window is in front. "
                                           + "Keep the desktop idle while UI tests run.");
        }
    }

    /// <summary>
    /// A mouse click goes to whatever is on top at the click point. Never click blind: bring the app to the front and
    /// check that the point really belongs to the app; if another window covers it (a person working on the
    /// machine, a notification), fail instead of clicking into that window.
    /// </summary>
    private void EnsureClickable()
    {
        var processId = ProcessId;
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            BringToFront();
            if (PointBelongsTo(processId))
            {
                return;
            }
            if (DateTime.UtcNow >= deadline)
            {
                throw new EnvironmentException($"Cannot click {Describe()}: another window covers it. Keep the desktop idle while UI tests run.");
            }
            Thread.Sleep(100);
        }
    }

    private bool PointBelongsTo(int processId)
    {
        try
        {
            var point = Element.TryGetClickablePoint(out var clickable) ? clickable : Center(Bounds);
            return Element.Automation.FromPoint(point).Properties.ProcessId.ValueOrDefault == processId;
        }
        catch (Exception e) when (Wait.IsTransient(e))
        {
            return false;
        }
    }

    private static Point Center(Rectangle r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>Mouse input goes to whatever is on top: bring the element's top-level window to the front first.</summary>
    private void BringToFront()
    {
        if (IsOffscreen)
        {
            ScrollIntoView();
        }
        var walker = Element.Automation.TreeWalkerFactory.GetControlViewWalker();
        AutomationElement? window = null;
        for (var current = Element; current != null; current = walker.GetParent(current))
        {
            if (current.Properties.ControlType.ValueOrDefault == ControlType.Window)
            {
                window = current;
            }
            if (current.Parent == null)
            {
                break;
            }
        }
        try
        {
            window?.AsWindow().SetForeground();
        }
        catch (Exception)
        {
            // best effort: the click still happens
        }
    }
}
