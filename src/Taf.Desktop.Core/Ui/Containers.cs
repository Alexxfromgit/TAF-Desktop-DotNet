using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Perf;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Ui;

/// <summary>Implemented by windows and components; base of mixin interfaces with default methods.</summary>
public interface IUiContainer
{
    string Name { get; }

    T Component<T>() where T : Component;
}

/// <summary>Common base of <see cref="Window"/> and <see cref="Component"/>: name, element injection context, helpers.</summary>
public abstract class UiContainer : IUiContainer
{
    /// <summary><c>MainWindow</c>, <c>MainWindow.details</c>, <c>MainWindow.products.rows[1]</c>.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Resolves the node elements are searched in (waits for it).</summary>
    internal Func<IUiNode> Context { get; private set; } = null!;

    internal List<UiElement> Identifiers { get; } = [];

    internal List<UiElement> GoneConditions { get; } = [];

    internal void Init(string name, Func<IUiNode> context)
    {
        Name = name;
        Context = context;
    }

    /// <summary>A component declared by a class-level [Locate], searched inside this container.</summary>
    public T Component<T>() where T : Component => WindowFactory.Component<T>(this);

    /// <summary>An element whose locator is only known at runtime: <c>Element("row", Locator.ByName(product))</c>.</summary>
    protected UiElement Element(string field, Locator locator) => UiElement.Within(Name, field, locator, Context);

    /// <summary>Waits until <typeparamref name="T"/> is ready - for navigation that is not a single click.</summary>
    protected static T Expect<T>() where T : Window => WindowFactory.On<T>();
}

/// <summary>A window of the app (main window, dialog, message box). Find it with <see cref="WindowFactory.On{T}"/>.</summary>
public abstract class Window : UiContainer
{
    /// <summary>The window element itself.</summary>
    public UiElement Root { get; internal set; } = null!;

    public string Title => Root.Text;

    /// <summary>True if the window and its identifiers exist right now.</summary>
    public bool IsOpen => Root.IsPresent && Identifiers.All(i => i.IsPresent);

    public void Close() => Step.Run($"Close {Name}", () => Root.WaitPresent().CloseWindow());

    /// <summary>Waits until the window is closed (e.g. after confirming a dialog).</summary>
    public void WaitClosed(TimeSpan? timeout = null) => Root.WaitGone(timeout);

    public byte[] Screenshot() => Root.WaitVisible().CapturePng();

    /// <summary>Waits until the window exists, its identifiers are visible and its [WaitForGone] elements are gone.</summary>
    internal void WaitReady()
    {
        var timeout = Wait.WindowTimeout;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (!Wait.Until(IsReady, timeout))
        {
            throw new WindowNotReadyException($"{Name} is not ready after {TafConfig.Format(timeout)}: {WhyNotReady()}");
        }
        PerfCollector.WindowReady(Name, watch.Elapsed);
    }

    private bool IsReady() =>
        Root.IsDisplayed && Identifiers.All(i => i.IsDisplayed) && GoneConditions.All(g => !g.IsDisplayed);

    private string WhyNotReady()
    {
        if (!Root.IsPresent)
        {
            return $"no window matches [{Root.Locator}]" + (Identifiers.Count > 0
                ? $" with {string.Join(", ", Identifiers.Select(i => i.Field))} inside"
                : "");
        }
        if (Identifiers.FirstOrDefault(i => !i.IsDisplayed) is { } missing)
        {
            return $"{missing.Name} is not visible [{missing.Locator}]";
        }
        if (GoneConditions.FirstOrDefault(g => g.IsDisplayed) is { } still)
        {
            return $"{still.Name} is still visible [{still.Locator}]";
        }
        return "the window is off screen";
    }
}

/// <summary>A part of a window with its own root (a panel, a list row); its elements are searched inside the root.</summary>
public abstract class Component : UiContainer
{
    public UiElement Root { get; internal set; } = null!;

    /// <summary>The root's text, e.g. the name of a list row.</summary>
    public string Text => Root.Text;

    public bool IsDisplayed => Root.IsDisplayed;
}

/// <summary>All matches of a locator: <c>[Locate(ControlType = ControlType.ListItem)] UiElements items;</c></summary>
public sealed class UiElements
{
    private readonly UiElement items;

    internal UiElements(UiElement items) => this.items = items;

    public string Name => items.Name;

    public int Count => items.FindAllNow().Count;

    public UiElement this[int index] => items.At(index);

    public IReadOnlyList<string> Texts => items.FindAllNow().Select(n => n.Text).ToList();

    public UiElements WaitAtLeast(int count, TimeSpan? timeout = null)
    {
        var limit = timeout ?? Wait.Timeout;
        if (!Wait.Until(() => Count >= count, limit))
        {
            throw new ElementNotFoundException($"{Name}: expected at least {count} elements, found {Count} after "
                                               + $"{TafConfig.Format(limit)} [{items.Locator}]");
        }
        return this;
    }
}

/// <summary>Repeated components, each scoped to its own root: <c>ComponentList&lt;ProductRow&gt; rows;</c></summary>
public sealed class ComponentList<T> where T : Component
{
    private readonly UiElement items;

    internal ComponentList(UiElement items) => this.items = items;

    public string Name => items.Name;

    public int Count => items.FindAllNow().Count;

    public T this[int index] => (T)WindowFactory.ComponentAt(typeof(T), items.At(index));

    public IReadOnlyList<T> All() => Enumerable.Range(0, Count).Select(i => this[i]).ToList();

    public T? Find(Func<T, bool> predicate) => All().FirstOrDefault(predicate);

    /// <summary>The first item matching <paramref name="predicate"/>; fails naming the list and <paramref name="description"/>.</summary>
    public T First(Func<T, bool> predicate, string description) =>
        Find(predicate) ?? throw new ElementNotFoundException($"{Name}: no item {description} among {Count} items [{items.Locator}]");

    public ComponentList<T> WaitAtLeast(int count, TimeSpan? timeout = null)
    {
        var limit = timeout ?? Wait.Timeout;
        if (!Wait.Until(() => Count >= count, limit))
        {
            throw new ElementNotFoundException($"{Name}: expected at least {count} items, found {Count} after "
                                               + $"{TafConfig.Format(limit)} [{items.Locator}]");
        }
        return this;
    }
}
