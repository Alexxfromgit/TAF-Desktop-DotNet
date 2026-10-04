using System.Text.RegularExpressions;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Examples.Windows;

[Locate(AutomationId = "MainWindow")]
public sealed partial class MainWindow : Window, IHasMainMenu
{
    [WindowIdentifier]
    [Locate(AutomationId = "WelcomeText")]
    private UiElement welcome = null!;

    [WaitForGone]
    [Locate(AutomationId = "LoadingIndicator")]
    private UiElement loading = null!;

    [Locate(AutomationId = "CartButton")]
    private UiElement cartButton = null!;

    [Locate(AutomationId = "SearchBox")]
    private UiElement search = null!;

    [Locate(AutomationId = "SearchButton")]
    private UiElement searchButton = null!;

    [Locate(AutomationId = "ErrorBanner")]
    private UiElement errorBanner = null!;

    [Locate(AutomationId = "RetryButton")]
    private UiElement retry = null!;

    [Locate(AutomationId = "StatusText")]
    private UiElement status = null!;

    private ProductList products = null!;

    private ProductDetails details = null!;

    public string Welcome => welcome.Text;

    public string Status => status.Text;

    public string Error => errorBanner.Text;

    public bool IsLoading => loading.IsDisplayed;

    public int CartCount => int.Parse(CartCountPattern().Match(cartButton.Text).Groups[1].Value);

    public IReadOnlyList<string> ProductNames => products.Names;

    public ProductDetails Details => details;

    /// <summary>Searches and waits until the list is loaded.</summary>
    public MainWindow Search(string text)
    {
        StartSearch(text);
        return WaitUntilLoaded();
    }

    /// <summary>Starts a search without waiting (to observe the loading state).</summary>
    public MainWindow StartSearch(string text)
    {
        search.Type(text);
        searchButton.Click();
        return this;
    }

    /// <summary>Waits until the status bar shows <paramref name="text"/> (the app finished an action).</summary>
    public MainWindow WaitStatus(string text)
    {
        status.WaitText(text);
        return this;
    }

    public MainWindow WaitUntilLoaded()
    {
        loading.WaitGone(TimeSpan.FromSeconds(15));
        return this;
    }

    public MainWindow Retry()
    {
        retry.Click();
        return WaitUntilLoaded();
    }

    public ProductDetails Select(string product)
    {
        products.Row(product).Root.Select();
        details.NameLabel.WaitText(product);
        return details;
    }

    public MainWindow AddToCart(string product, int quantity = 1)
    {
        var count = CartCount;
        Select(product).SetQuantity(quantity).AddToCart();
        cartButton.WaitText($"Cart ({count + quantity})");
        return this;
    }

    public CartWindow OpenCart() => cartButton.ClickAndExpect<CartWindow>();

    public MessageBoxDialog OpenAbout()
    {
        this.Menu.Open("Help", "About ShopDesk");
        return Expect<MessageBoxDialog>();
    }

    /// <summary>File &gt; Export cart...: a save dialog, or a message box if the cart is empty.</summary>
    public void StartExport() => this.Menu.Open("File", "Export cart...");

    public LoginWindow SignOut()
    {
        this.Menu.Open("File", "Sign out");
        return Expect<LoginWindow>();
    }

    [GeneratedRegex(@"\((\d+)\)")]
    private static partial Regex CartCountPattern();
}

/// <summary>The product list; each row is a component.</summary>
[Locate(AutomationId = "ProductList")]
public sealed class ProductList : Component
{
    [Locate(ControlType = FlaUI.Core.Definitions.ControlType.ListItem)]
    private ComponentList<ProductRow> rows = null!;

    public IReadOnlyList<string> Names => rows.All().Select(r => r.ProductName).ToList();

    public ProductRow Row(string product) =>
        rows.WaitAtLeast(1).First(r => r.ProductName == product, $"named '{product}'");
}

/// <summary>A product row: <c>Canvas Backpack - $29.99</c>.</summary>
public sealed class ProductRow : Component
{
    public string ProductName => Text.Split(" - ")[0];

    public string Price => Text.Split(" - ")[^1];
}

/// <summary>The details panel of the selected product.</summary>
[Locate(AutomationId = "DetailsPanel")]
public sealed class ProductDetails : Component
{
    [Locate(AutomationId = "DetailsName")]
    private UiElement name = null!;

    [Locate(AutomationId = "DetailsPrice")]
    private UiElement price = null!;

    [Locate(AutomationId = "DetailsStock")]
    private UiElement stock = null!;

    [Locate(AutomationId = "QuantityBox")]
    private UiElement quantity = null!;

    [Locate(AutomationId = "AddToCartButton")]
    private UiElement addToCart = null!;

    public UiElement NameLabel => name;

    public string Price => price.Text;

    public string Stock => stock.Text;

    public bool CanAddToCart => addToCart.IsEnabled;

    public ProductDetails SetQuantity(int count)
    {
        quantity.Type(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return this;
    }

    public void AddToCart() => addToCart.Click();
}
