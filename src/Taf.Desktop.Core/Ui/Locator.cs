using FlaUI.Core.Definitions;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Ui;

public enum LocatorKind
{
    AutomationId,
    Name,
    NameContains,
    ClassName,
    XPath,
    ControlType,
}

/// <summary>How to find an element: one strategy, optionally narrowed to a control type.</summary>
public sealed record Locator(LocatorKind Kind, string Value, ControlType? ControlType = null)
{
    public static Locator ById(string automationId, ControlType? type = null) => new(LocatorKind.AutomationId, automationId, type);

    public static Locator ByName(string name, ControlType? type = null) => new(LocatorKind.Name, name, type);

    public static Locator ByNameContaining(string text, ControlType? type = null) => new(LocatorKind.NameContains, text, type);

    public static Locator ByClassName(string className, ControlType? type = null) => new(LocatorKind.ClassName, className, type);

    public static Locator ByXPath(string xpath) => new(LocatorKind.XPath, xpath);

    public static Locator Of(ControlType type) => new(LocatorKind.ControlType, "", type);

    /// <summary>Matches every element (used to walk the tree).</summary>
    public static Locator Any { get; } = new(LocatorKind.ControlType, "");

    /// <summary>True if <paramref name="node"/> satisfies this locator (XPath cannot be checked per node).</summary>
    public bool Matches(IUiNode node)
    {
        if (ControlType is { } type && node.ControlType != type)
        {
            return false;
        }
        return Kind switch
        {
            LocatorKind.AutomationId => node.AutomationId == Value,
            LocatorKind.Name => node.Name == Value,
            LocatorKind.NameContains => node.Name.Contains(Value, StringComparison.OrdinalIgnoreCase),
            LocatorKind.ClassName => node.ClassName == Value,
            _ => true,
        };
    }

    public override string ToString()
    {
        var strategy = Kind == LocatorKind.ControlType ? "" : $"{Kind}={(Kind is LocatorKind.Name or LocatorKind.NameContains ? $"'{Value}'" : Value)}";
        var type = ControlType is { } t ? $"ControlType={t}" : "";
        return string.Join(", ", new[] { strategy, type }.Where(s => s.Length > 0));
    }
}

/// <summary>
/// Locates a window (on the class), a component (on the class or the field) or an element (on the field). Use
/// exactly one of AutomationId, Name, NameContains, ClassName or XPath, optionally with ControlType - or only
/// ControlType ("the only list in this component").
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class LocateAttribute : Attribute
{
    public string? AutomationId { get; init; }

    public string? Name { get; init; }

    public string? NameContains { get; init; }

    public string? ClassName { get; init; }

    public string? XPath { get; init; }

    /// <summary>Narrows the match to a control type; <see cref="FlaUI.Core.Definitions.ControlType.Unknown"/> = any.</summary>
    public ControlType ControlType { get; init; } = ControlType.Unknown;

    /// <summary>The locator, or a <see cref="FrameworkException"/> naming <paramref name="where"/> if ambiguous.</summary>
    public Locator ToLocator(string where)
    {
        var strategies = new (LocatorKind Kind, string? Value)[]
        {
            (LocatorKind.AutomationId, AutomationId), (LocatorKind.Name, Name), (LocatorKind.NameContains, NameContains),
            (LocatorKind.ClassName, ClassName), (LocatorKind.XPath, XPath),
        }.Where(s => !string.IsNullOrEmpty(s.Value)).ToList();
        ControlType? type = ControlType == ControlType.Unknown ? null : ControlType;
        if (strategies.Count == 0 && type is { } only)
        {
            return Locator.Of(only);
        }
        if (strategies.Count != 1)
        {
            throw new FrameworkException($"{where}: [Locate] needs exactly one of AutomationId, Name, NameContains, ClassName "
                                         + $"or XPath (or only ControlType), found {strategies.Count}");
        }
        if (strategies[0].Kind == LocatorKind.XPath && type != null)
        {
            throw new FrameworkException($"{where}: [Locate] cannot combine XPath with ControlType; put the type into the XPath");
        }
        return new Locator(strategies[0].Kind, strategies[0].Value!, type);
    }
}

/// <summary>Marks an element that proves its window is open; waited for by <c>WindowFactory.On</c>.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class WindowIdentifierAttribute : Attribute;

/// <summary>Marks an element (a loading indicator, a progress bar) that must disappear before the window is ready.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class WaitForGoneAttribute : Attribute;
