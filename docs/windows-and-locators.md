# Windows, components and locators

## Windows

```csharp
[Locate(AutomationId = "CartWindow")]          // finds the window among the app's windows
public sealed class CartWindow : Window
{
    [WindowIdentifier]                         // proves the window is open
    [Locate(AutomationId = "TotalText")]
    private UiElement total = null!;

    [WaitForGone]                              // must disappear before the window counts as ready
    [Locate(AutomationId = "LoadingIndicator")]
    private UiElement loading = null!;

    [Locate(AutomationId = "PlaceOrderButton")]
    private UiElement placeOrder = null!;

    public MessageBoxDialog PlaceOrder() => placeOrder.ClickAndExpect<MessageBoxDialog>();
}
```

- `On<CartWindow>()` (in tests) or `WindowFactory.On<CartWindow>()` waits up to `Wait:WindowTimeout` until the window
  exists, its identifiers are visible and its `[WaitForGone]` elements are gone. The time goes to the performance
  report.
- Windows are searched among the top-level windows of the app process and the dialogs they own.
- Several windows can match the class locator; for example, every Win32 dialog has the class `#32770`. The
  identifiers then decide which one is meant.
- `element.ClickAndExpect<T>()` clicks, waits for the next window and records the transition time.

## Locators

`[Locate]` takes exactly one of `AutomationId`, `Name`, `NameContains`, `ClassName` or `XPath`. Any of them except
XPath can be narrowed with `ControlType`, and `ControlType` alone means "the only list in this component".

| App technology | AutomationId comes from |
|---|---|
| WPF | `AutomationProperties.AutomationId`, otherwise `x:Name` is **not** used |
| WinForms | the control's `Name` |
| UWP / WinUI | `AutomationProperties.AutomationId` |
| Win32 | the control id (`1001`, `65535`, ...) |

Ask developers for stable AutomationIds. They survive layout changes and translations, while names do not.

## Components and lists

```csharp
[Locate(AutomationId = "ProductList")]
public sealed class ProductList : Component
{
    [Locate(ControlType = ControlType.ListItem)]
    private ComponentList<ProductRow> rows = null!;

    public ProductRow Row(string product) => rows.WaitAtLeast(1).First(r => r.ProductName == product, $"named '{product}'");
}

public sealed class ProductRow : Component
{
    public string ProductName => Text.Split(" - ")[0];
}
```

- A component's elements are searched inside its root.
- A component used as a field (`private ProductList products;`) takes its `[Locate]` from the class.
- Each item of a `ComponentList<T>` is a component with its own root (`MainWindow.products.rows[2]`).
- `UiElements` gives all matches of a locator, for plain elements.
- `Element("row", Locator.ByName(product))` creates an element at runtime, for locators known only during the test.

## Mixins

Shared parts such as a menu bar or a status bar become mixins through C# 14 extension properties, so no cast is
needed:

```csharp
public interface IHasMainMenu : IUiContainer;

public static class HasMainMenuMixin
{
    extension(IHasMainMenu window)
    {
        public MainMenu Menu => window.Component<MainMenu>();
    }
}

public sealed class MainWindow : Window, IHasMainMenu { ... }      // mainWindow.Menu.Open("File", "Export cart...")
```

## Elements

`UiElement` finds its element again on every call, with an explicit wait (`Wait:Timeout`, polling `Wait:Poll`), so
there are no stale references. Every action is a report step.

| | |
|---|---|
| `Click()`, `DoubleClick()`, `RightClick()` | `Ui:ClickMode`: `Auto` (Invoke for WPF/XAML, mouse for WinForms/Win32), `Invoke`, `Mouse` |
| `Type(text)`, `Clear()` | through UI Automation when the field accepts it, otherwise typed. Password fields are masked in the report. |
| `TypeKeys(text)` | always typed on the keyboard, for controls that ignore values set through UI Automation (file dialogs) |
| `Check()`, `Uncheck()`, `SetChecked(bool)` | toggle pattern; only toggles when needed |
| `Select(item)` / `Select()` | an item of a combo box or list (expands and collapses as needed) / this element itself |
| `OpenMenu("File", "Export cart...")` | menu bars, including WinForms drop-downs that open as separate popup windows |
| `Text`, `Value`, `Items`, `IsEnabled`, `IsChecked` | wait for the element, then read |
| `IsPresent`, `IsDisplayed` | no waiting |
| `WaitVisible()`, `WaitGone()`, `WaitText(text)`, `WaitUntil(condition, description)` | explicit waits with readable failures |

**Why mouse clicks for WinForms:** an Invoke executes the click handler inside the UI Automation call. If the
handler opens a modal dialog (`MessageBox.Show`, `ShowDialog`), the app's UI Automation provider is blocked until
the dialog closes, so the test can't even see the dialog. A mouse click doesn't have this problem. For WPF, the
framework also continues after `Ui:InvokeReturnTimeout` if an Invoke doesn't return.

## Dialogs

```csharp
var message = On<MessageBoxDialog>();     // any MessageBox.Show / Win32 MessageBox
message.Message.Should().Be("Your cart is empty");
message.Ok();                             // independent of the Windows language

On<FileDialog>().Choose(@"C:\temp\cart.csv");   // Windows open/save dialogs
```
