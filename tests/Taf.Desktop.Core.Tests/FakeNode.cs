using System.Drawing;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Tests;

/// <summary>An in-memory UI Automation node: build trees with <see cref="Add"/>, inspect <see cref="Actions"/>.</summary>
internal sealed class FakeNode(ControlType type, string automationId = "", string name = "", string className = "", string frameworkId = "WPF")
    : IUiNode
{
    private readonly List<FakeNode> children = [];

    public List<string> Actions { get; } = [];

    public Action<FakeNode>? OnInvoke { get; set; }

    public Action<FakeNode>? OnExpand { get; set; }

    public FakeNode? Parent { get; private set; }

    public string Name { get; set; } = name;

    public string AutomationId { get; set; } = automationId;

    public string ClassName { get; set; } = className;

    public ControlType ControlType { get; set; } = type;

    public string FrameworkId { get; set; } = frameworkId;

    public int ProcessId => 42;

    public bool IsOffscreen { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsPassword { get; set; }

    public bool IsReadOnly { get; set; }

    public Rectangle Bounds => new(0, 0, 100, 20);

    public string Text => ControlType is ControlType.Edit or ControlType.ComboBox ? Value ?? Name : Name;

    public string? Value { get; set; }

    public bool? IsToggled { get; set; }

    public bool? IsSelected { get; set; }

    public bool? IsExpanded { get; set; }

    public bool SupportsInvoke { get; set; } = true;

    /// <summary>Like a password box that refuses UI Automation values.</summary>
    public bool RejectsSetValue { get; set; }

    public bool SupportsValue => Value != null;

    public bool SupportsSelect => IsSelected != null;

    public bool CanExpand => IsExpanded != null;

    public FakeNode Add(params FakeNode[] nodes)
    {
        foreach (var node in nodes)
        {
            node.Parent = this;
            children.Add(node);
        }
        return this;
    }

    public void Remove(FakeNode node) => children.Remove(node);

    public IReadOnlyList<IUiNode> FindAll(Locator locator, bool childrenOnly = false)
    {
        if (locator.Kind == LocatorKind.XPath)
        {
            throw new NotSupportedException("XPath is not supported by fake nodes");
        }
        return (childrenOnly ? children : Descendants()).Where(locator.Matches).Cast<IUiNode>().ToList();
    }

    public void Invoke()
    {
        Actions.Add("invoke");
        OnInvoke?.Invoke(this);
    }

    public void MouseClick()
    {
        Actions.Add("mouse");
        OnInvoke?.Invoke(this);
    }

    public void DoubleClick() => Actions.Add("double");

    public void RightClick() => Actions.Add("right");

    public void Focus() => Actions.Add("focus");

    public void SetValue(string value)
    {
        Actions.Add($"set:{value}");
        if (RejectsSetValue)
        {
            throw new InvalidOperationException("SetValue is not allowed");
        }
        Value = value;
    }

    public void TypeText(string text)
    {
        Actions.Add($"type:{text}");
        Value = text;
    }

    public void PressKey(VirtualKeyShort key) => Actions.Add($"key:{key}");

    public void Toggle()
    {
        Actions.Add("toggle");
        IsToggled = !IsToggled;
    }

    public void Select()
    {
        Actions.Add("select");
        IsSelected = true;
    }

    public void Expand()
    {
        Actions.Add("expand");
        IsExpanded = true;
        OnExpand?.Invoke(this);
    }

    public void Collapse()
    {
        Actions.Add("collapse");
        IsExpanded = false;
    }

    public void ScrollIntoView() => Actions.Add("scroll");

    public void CloseWindow() => Actions.Add("close");

    public byte[] CapturePng() => [1, 2, 3];

    private IEnumerable<FakeNode> Descendants() => children.SelectMany(c => new[] { c }.Concat(c.Descendants()));
}
