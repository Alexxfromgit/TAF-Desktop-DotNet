using System.Reflection;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Ui;

/// <summary>
/// Creates windows and components and injects their [Locate] fields: <see cref="UiElement"/>, <see cref="UiElements"/>,
/// <see cref="Component"/> subclasses and <see cref="ComponentList{T}"/>. Creation is cheap - nothing is looked up
/// until used.
/// </summary>
public static class WindowFactory
{
    /// <summary>The window of type <typeparamref name="T"/> once it is ready (see <see cref="Window"/>).</summary>
    public static T On<T>() where T : Window
    {
        var window = Create<T>(UiRuntime.Windows);
        window.WaitReady();
        return window;
    }

    /// <summary>A window found among <paramref name="windows"/> (not waited for) - used by unit tests with fake trees.</summary>
    public static T Create<T>(Func<IReadOnlyList<IUiNode>> windows) where T : Window
    {
        var type = typeof(T);
        var locate = type.GetCustomAttribute<LocateAttribute>()
                     ?? throw new FrameworkException($"{type.Name} needs a class-level [Locate] that identifies its window, "
                                                     + "e.g. [Locate(AutomationId = \"MainWindow\")]");
        var locator = locate.ToLocator(type.Name);
        if (locator.Kind == LocatorKind.XPath)
        {
            throw new FrameworkException($"{type.Name}: windows cannot be located by XPath");
        }
        var window = Instantiate<T>(type);
        // Several windows can match the class locator (all Win32 dialogs are #32770): the identifiers decide.
        var root = new UiElement(type.Name, "", locator, () => windows()
            .Where(locator.Matches)
            .Where(candidate => window.Identifiers.All(id => Wait.Check(() => candidate.FindAll(id.Locator).Count > 0)))
            .ToList(), cacheNode: true);
        window.Root = root;
        window.Init(type.Name, () => root.WaitPresent());
        Inject(window);
        return window;
    }

    internal static T Component<T>(UiContainer parent) where T : Component
    {
        var type = typeof(T);
        var locate = type.GetCustomAttribute<LocateAttribute>()
                     ?? throw new FrameworkException($"{type.Name} needs a class-level [Locate] to be used with Component<{type.Name}>(). "
                                                     + "Alternatively declare it as a [Locate] field.");
        var field = char.ToLowerInvariant(type.Name[0]) + type.Name[1..];
        return (T)ComponentAt(type, UiElement.Within(parent.Name, field, locate.ToLocator($"{parent.Name}.{field}"), parent.Context));
    }

    internal static Component ComponentAt(Type type, UiElement root)
    {
        var component = (Component)Instantiate<object>(type);
        component.Root = root;
        component.Init(root.Name, () => root.WaitPresent());
        Inject(component);
        return component;
    }

    private static void Inject(UiContainer container)
    {
        for (var type = container.GetType();
             type != null && type != typeof(Window) && type != typeof(Component) && type != typeof(UiContainer);
             type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (IsInjectable(field.FieldType) && !field.Name.StartsWith('<'))
                {
                    field.SetValue(container, ValueFor(container, field));
                }
            }
        }
    }

    private static object ValueFor(UiContainer container, FieldInfo field)
    {
        var where = $"{container.Name}.{field.Name}";
        var fieldType = field.FieldType;
        var locate = field.GetCustomAttribute<LocateAttribute>()
                     ?? (typeof(Component).IsAssignableFrom(fieldType) ? fieldType.GetCustomAttribute<LocateAttribute>() : null)
                     ?? throw new FrameworkException($"{where} needs [Locate]");
        var element = UiElement.Within(container.Name, field.Name, locate.ToLocator(where), container.Context);
        if (field.GetCustomAttribute<WindowIdentifierAttribute>() != null)
        {
            container.Identifiers.Add(element);
        }
        if (field.GetCustomAttribute<WaitForGoneAttribute>() != null)
        {
            container.GoneConditions.Add(element);
        }
        if (fieldType == typeof(UiElement))
        {
            return element;
        }
        if (fieldType == typeof(UiElements))
        {
            return new UiElements(element);
        }
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ComponentList<>))
        {
            return Activator.CreateInstance(fieldType, BindingFlags.Instance | BindingFlags.NonPublic, null, [element], null)!;
        }
        return ComponentAt(fieldType, element);
    }

    private static bool IsInjectable(Type type) =>
        type == typeof(UiElement) || type == typeof(UiElements) || typeof(Component).IsAssignableFrom(type)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ComponentList<>));

    private static T Instantiate<T>(Type type)
    {
        try
        {
            return (T)Activator.CreateInstance(type, nonPublic: true)!;
        }
        catch (MissingMethodException e)
        {
            throw new FrameworkException($"{type.Name} needs a parameterless constructor", e);
        }
    }
}
