using System.Diagnostics;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Perf;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Ui;

/// <summary>
/// A lazily located element. It stores how to find the element, never the element itself: every call finds it again
/// with an explicit wait, so there are no stale references. Its name (<c>CartWindow.placeOrder</c>) appears in report
/// steps and failures, together with the locator.
/// </summary>
public class UiElement
{
    private readonly Func<IReadOnlyList<IUiNode>> find;
    private readonly bool cacheNode;
    private IUiNode? cached;

    internal UiElement(string owner, string field, Locator locator, Func<IReadOnlyList<IUiNode>> find, int? index = null,
        bool cacheNode = false)
    {
        Owner = owner;
        Field = field;
        Locator = locator;
        this.find = find;
        Index = index;
        this.cacheNode = cacheNode;
    }

    /// <summary>Window or component the element belongs to, e.g. <c>CartWindow</c>.</summary>
    public string Owner { get; }

    /// <summary>Field name, e.g. <c>placeOrder</c>; empty for a window itself.</summary>
    public string Field { get; }

    public Locator Locator { get; }

    /// <summary>Position among all matches (lists), or null for the first match.</summary>
    public int? Index { get; }

    /// <summary><c>CartWindow.placeOrder</c>, <c>MainWindow.products.rows[2]</c>.</summary>
    public string Name => (Field.Length == 0 ? Owner : $"{Owner}.{Field}") + (Index is { } i ? $"[{i}]" : "");

    // ---------------------------------------------------------------- queries (no waiting)

    /// <summary>True if the element exists right now (it may be off screen).</summary>
    public bool IsPresent => Safe(() => FindNow() != null);

    /// <summary>True if the element exists and is on screen right now.</summary>
    public bool IsDisplayed => Safe(() => FindNow() is { IsOffscreen: false });

    // ---------------------------------------------------------------- queries (waiting for the element)

    /// <summary>What a user reads: the value of edits and combo boxes, otherwise the name.</summary>
    public string Text => WaitVisible().Text;

    public string Value => WaitVisible().Value ?? "";

    public bool IsEnabled => WaitVisible().IsEnabled;

    public bool IsChecked => WaitVisible().IsToggled ?? throw new FrameworkException($"{Name} is not a check box or toggle button");

    public bool IsSelected => WaitVisible().IsSelected ?? throw new FrameworkException($"{Name} cannot be selected");

    /// <summary>Names of the items of a list or combo box.</summary>
    public IReadOnlyList<string> Items => Step.Run($"Read the items of {Name}", () =>
    {
        var node = WaitVisible();
        var expanded = ExpandIfNeeded(node);
        try
        {
            return ItemNodes(node).Select(i => i.Name).ToList();
        }
        finally
        {
            if (expanded)
            {
                node.Collapse();
            }
        }
    });

    // ---------------------------------------------------------------- actions (each one is a report step)

    public void Click() => Step.Run($"Click {Name}", () => Clicker.Click(WaitEnabled()));

    /// <summary>Clicks and waits until <typeparamref name="T"/> is ready; the transition time goes to the performance report.</summary>
    public T ClickAndExpect<T>() where T : Window => Step.Run($"Click {Name} and expect {typeof(T).Name}", () =>
    {
        var node = WaitEnabled();
        var watch = Stopwatch.StartNew();
        Clicker.Click(node);
        var next = WindowFactory.On<T>();
        PerfCollector.Transition(Owner, Field, typeof(T).Name, watch.Elapsed);
        return next;
    });

    public void DoubleClick() => Step.Run($"Double-click {Name}", () => WaitEnabled().DoubleClick());

    public void RightClick() => Step.Run($"Right-click {Name}", () => WaitEnabled().RightClick());

    /// <summary>
    /// Replaces the content: through UI Automation when the field accepts it (checked for normal fields), otherwise by
    /// typing. Passwords are masked in the report.
    /// </summary>
    public void Type(string text)
    {
        var masked = Field.Contains("password", StringComparison.OrdinalIgnoreCase) || Safe(() => FindNow()?.IsPassword == true);
        Step.Run(masked ? $"Type '****' into {Name}" : $"Type '{text}' into {Name}", () =>
        {
            var node = WaitEnabled();
            if (node.SupportsValue && !node.IsReadOnly && TrySetValue(node, text))
            {
                return;
            }
            node.TypeText(text);
        });
    }

    private static bool TrySetValue(IUiNode node, string text)
    {
        try
        {
            node.SetValue(text);
            return node.IsPassword || node.Value == text;
        }
        catch (Exception e) when (e is not ElementNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the content by typing on the keyboard, like a user. For controls that ignore values set through UI
    /// Automation (the file name box of Windows file dialogs).
    /// </summary>
    public void TypeKeys(string text) => Step.Run($"Type '{text}' into {Name} (keyboard)", () => WaitEnabled().TypeText(text));

    public void Clear() => Step.Run($"Clear {Name}", () =>
    {
        var node = WaitEnabled();
        if (node.SupportsValue && !node.IsReadOnly && !node.IsPassword)
        {
            node.SetValue("");
        }
        else
        {
            node.TypeText("");
        }
    });

    public void Press(VirtualKeyShort key) => Step.Run($"Press {key} in {Name}", () => WaitEnabled().PressKey(key));

    public void Focus() => Step.Run($"Focus {Name}", () => WaitVisible().Focus());

    public void Check() => SetChecked(true);

    public void Uncheck() => SetChecked(false);

    public void SetChecked(bool value) => Step.Run($"{(value ? "Check" : "Uncheck")} {Name}", () =>
    {
        var node = WaitEnabled();
        if (node.IsToggled is null)
        {
            throw new FrameworkException($"{Name} is not a check box or toggle button");
        }
        if (node.IsToggled != value)
        {
            node.Toggle();
        }
    });

    /// <summary>Selects this element itself, e.g. a list row.</summary>
    public void Select() => Step.Run($"Select {Name}", () =>
    {
        var node = WaitVisible();
        if (node.SupportsSelect)
        {
            node.Select();
        }
        else
        {
            Clicker.Click(node);
        }
    });

    /// <summary>Selects the item named <paramref name="item"/> in a list or combo box (expanding it if needed).</summary>
    public void Select(string item) => Step.Run($"Select '{item}' in {Name}", () =>
    {
        var node = WaitEnabled();
        var expanded = ExpandIfNeeded(node);
        var target = Wait.For(() => FindItem(node, item));
        if (target == null)
        {
            var available = ItemNodes(node).Select(i => i.Name).ToList();
            if (expanded)
            {
                node.Collapse();
            }
            throw new ElementNotFoundException($"{Name} has no item '{item}' after {TafConfig.Format(Wait.Timeout)} "
                                               + $"[items: {string.Join(", ", available)}]");
        }
        if (target.SupportsSelect)
        {
            target.Select();
        }
        else
        {
            Clicker.Click(target);
        }
        if (expanded && Wait.Check(() => node.IsExpanded == true))
        {
            node.Collapse();
        }
    });

    /// <summary>Opens a menu path on a menu bar element: <c>menu.OpenMenu("File", "Export cart...")</c>.</summary>
    public void OpenMenu(params string[] path) => Step.Run($"Open menu {string.Join(" > ", path)}", () =>
    {
        IUiNode container = WaitVisible();
        for (var i = 0; i < path.Length; i++)
        {
            var segment = path[i];
            var parent = container;
            var item = Wait.For(() => parent.FindAll(Locator.ByName(segment, ControlType.MenuItem)).FirstOrDefault()
                                      ?? UiRuntime.Popups().SelectMany(p => p.FindAll(Locator.ByName(segment, ControlType.MenuItem))).FirstOrDefault())
                       ?? throw new ElementNotFoundException($"{Name}: menu item '{segment}' of {string.Join(" > ", path)} not found "
                                                             + $"after {TafConfig.Format(Wait.Timeout)}");
            if (i < path.Length - 1 && item.CanExpand)
            {
                item.Expand();
            }
            else
            {
                Clicker.Click(item);
            }
            container = item;
        }
    });

    public void ScrollIntoView() => Step.Run($"Scroll {Name} into view", () => WaitPresent().ScrollIntoView());

    public byte[] Screenshot() => WaitVisible().CapturePng();

    // ---------------------------------------------------------------- waits

    /// <summary>The element once it exists and is on screen.</summary>
    public IUiNode WaitVisible(TimeSpan? timeout = null) => WaitFor(n => !n.IsOffscreen, "visible", timeout);

    /// <summary>The element once it exists (on or off screen).</summary>
    public IUiNode WaitPresent(TimeSpan? timeout = null) => WaitFor(_ => true, "present", timeout);

    /// <summary>The element once it is visible and enabled.</summary>
    public IUiNode WaitEnabled(TimeSpan? timeout = null)
    {
        var node = WaitVisible(timeout);
        if (node.IsEnabled)
        {
            return node;
        }
        var limit = timeout ?? Wait.Timeout;
        if (!Wait.Until(() => FindNow() is { IsEnabled: true }, limit))
        {
            throw new PotentialDefectException($"{Name} is visible but not enabled after {TafConfig.Format(limit)} [{Locator}]");
        }
        return FindNow()!;
    }

    /// <summary>Waits until the element is gone or off screen (a loading indicator, a closed dialog).</summary>
    public UiElement WaitGone(TimeSpan? timeout = null)
    {
        var limit = timeout ?? Wait.Timeout;
        if (!Wait.Until(() => FindNow() is not { IsOffscreen: false }, limit))
        {
            throw new PotentialDefectException($"{Name} is still visible after {TafConfig.Format(limit)} [{Locator}]");
        }
        return this;
    }

    /// <summary>Waits for a condition on the element: <c>status.WaitUntil(e => e.Text == "3 products", "shows 3 products")</c>.</summary>
    public UiElement WaitUntil(Func<UiElement, bool> condition, string description, TimeSpan? timeout = null)
    {
        var limit = timeout ?? Wait.Timeout;
        if (!Wait.Until(() => condition(this), limit))
        {
            var text = Safe(() => FindNow()?.Text);
            throw new PotentialDefectException($"{Name} {description}: still not the case after {TafConfig.Format(limit)}"
                                               + (text is null ? " (element not found)" : $" (text: '{text}')"));
        }
        return this;
    }

    /// <summary>Waits until the element shows <paramref name="expected"/>.</summary>
    public UiElement WaitText(string expected, TimeSpan? timeout = null) =>
        WaitUntil(e => e.IsDisplayed && e.FindNow()?.Text == expected, $"shows '{expected}'", timeout);

    public override string ToString() => $"{Name} [{Locator}]";

    // ---------------------------------------------------------------- internals

    internal UiElement At(int index) => new(Owner, Field, Locator, find, index);

    internal static UiElement Within(string owner, string field, Locator locator, Func<IUiNode> context) =>
        new(owner, field, locator, () => context().FindAll(locator));

    internal IReadOnlyList<IUiNode> FindAllNow()
    {
        try
        {
            return find();
        }
        catch (Exception e) when (Wait.IsTransient(e))
        {
            return [];
        }
    }

    internal IUiNode? FindNow()
    {
        // Windows are cached: re-reading a property of a closed window fails, which triggers a new search.
        if (cacheNode && cached is { } known && Wait.Check(() => Locator.Matches(known)))
        {
            return known;
        }
        var all = FindAllNow();
        var found = Index is { } i ? all.ElementAtOrDefault(i) : all.FirstOrDefault();
        if (cacheNode)
        {
            cached = found;
        }
        return found;
    }

    private IUiNode WaitFor(Func<IUiNode, bool> state, string what, TimeSpan? timeout)
    {
        var limit = timeout ?? Wait.Timeout;
        var watch = Stopwatch.StartNew();
        IUiNode? found = null;
        if (!Wait.Until(() => (found = FindNow()) is { } n && state(n), limit))
        {
            throw new ElementNotFoundException($"{Name} is not {what} after {TafConfig.Format(limit)} [{Locator}]");
        }
        PerfCollector.Lookup(Name, watch.Elapsed);
        return found!;
    }

    private static bool ExpandIfNeeded(IUiNode node)
    {
        if (!node.CanExpand || node.ControlType == ControlType.MenuItem || node.IsExpanded == true)
        {
            return false;
        }
        node.Expand();
        return true;
    }

    private static IReadOnlyList<IUiNode> ItemNodes(IUiNode node)
    {
        var items = node.FindAll(Locator.Of(ControlType.ListItem));
        return items.Count > 0 ? items : UiRuntime.Popups().SelectMany(p => p.FindAll(Locator.Of(ControlType.ListItem))).ToList();
    }

    private static IUiNode? FindItem(IUiNode node, string item) =>
        ItemNodes(node).FirstOrDefault(i => i.Name == item);

    private static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (Wait.IsTransient(e) || e is ElementNotFoundException)
        {
            return default;
        }
    }
}

/// <summary>Clicks according to <c>Ui:ClickMode</c>.</summary>
internal static class Clicker
{
    public static void Click(IUiNode node)
    {
        var mode = TafConfig.Current.Enum("Ui:ClickMode", ClickMode.Auto);
        if (mode == ClickMode.Auto)
        {
            mode = node.FrameworkId is "WPF" or "XAML" && node.SupportsInvoke ? ClickMode.Invoke : ClickMode.Mouse;
        }
        if (mode == ClickMode.Mouse || !node.SupportsInvoke)
        {
            node.MouseClick();
            return;
        }
        // Invoke returns only after the click handler: when it opens a modal dialog, continue without waiting.
        var call = Task.Run(node.Invoke);
        if (call.Wait(TafConfig.Current.Duration("Ui:InvokeReturnTimeout")))
        {
            return;
        }
        _ = call.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
}
