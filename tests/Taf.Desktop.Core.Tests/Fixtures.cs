using FlaUI.Core.Definitions;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Tests;

// Windows and components of an imaginary shop app, used by the window-model tests.

[Locate(AutomationId = "MainWindow")]
public sealed class ShopWindow : Window, IHasMenu
{
    [WindowIdentifier]
    [Locate(AutomationId = "ProductList")]
    private UiElement productList = null!;

    [WaitForGone]
    [Locate(AutomationId = "Loading")]
    private UiElement loading = null!;

    [Locate(AutomationId = "SearchBox")]
    private UiElement search = null!;

    [Locate(AutomationId = "PasswordBox")]
    private UiElement password = null!;

    [Locate(AutomationId = "GiftWrap")]
    private UiElement giftWrap = null!;

    [Locate(AutomationId = "Shipping")]
    private UiElement shipping = null!;

    [Locate(AutomationId = "SignIn")]
    private UiElement signIn = null!;

    [Locate(AutomationId = "Details")]
    private DetailsPanel details = null!;

    [Locate(ControlType = ControlType.ListItem)]
    private ComponentList<ProductRow> rows = null!;

    public UiElement ProductList => productList;

    public UiElement Loading => loading;

    public UiElement Search => search;

    public UiElement Password => password;

    public UiElement GiftWrap => giftWrap;

    public UiElement Shipping => shipping;

    public UiElement SignIn => signIn;

    public DetailsPanel Details => details;

    public ComponentList<ProductRow> Rows => rows;
}

public sealed class DetailsPanel : Component
{
    [Locate(AutomationId = "Price")]
    private UiElement price = null!;

    public UiElement Price => price;
}

public sealed class ProductRow : Component
{
    public string Title => Text.Split(" - ")[0];
}

[Locate(AutomationId = "MainMenu")]
public sealed class MainMenu : Component
{
    public void Open(params string[] path) => Root.OpenMenu(path);
}

/// <summary>Mixin: windows with the main menu. The C# 14 extension property needs no cast on the window.</summary>
public interface IHasMenu : IUiContainer;

public static class HasMenuMixin
{
    extension(IHasMenu window)
    {
        public MainMenu Menu => window.Component<MainMenu>();
    }
}

[Locate(AutomationId = "OtherWindow")]
public sealed class NoIdentifierWindow : Window;

public sealed class NotLocatedWindow : Window
{
    [WindowIdentifier]
    [Locate(AutomationId = "x")]
    private UiElement x = null!;

    public UiElement X => x;
}

[Locate(AutomationId = "Ambiguous")]
public sealed class AmbiguousWindow : Window
{
    [Locate(AutomationId = "a", Name = "b")]
    private UiElement both = null!;

    public UiElement Both => both;
}
