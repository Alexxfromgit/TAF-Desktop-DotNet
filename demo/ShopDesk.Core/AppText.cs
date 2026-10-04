namespace ShopDesk;

/// <summary>User-visible texts shared by both flavours, so the same tests run against WPF and WinForms.</summary>
public static class AppText
{
    public const string LoginTitle = "ShopDesk - Sign in";
    public const string MainTitle = "ShopDesk";
    public const string CartTitle = "Cart";
    public const string AboutTitle = "About ShopDesk";
    public const string AboutText = "ShopDesk 1.0\nA demo app for desktop test automation.";
    public const string ExportTitle = "Export cart";
    public const string EmptyCart = "Your cart is empty";
    public const string NoProducts = "No products found";
    public const string OrderPlacedTitle = "Order placed";
    public const string OrderFailedTitle = "Order failed";

    public static string Welcome(string displayName) => $"Signed in as {displayName}";

    public static string ProductCount(int count) => count == 1 ? "1 product" : $"{count} products";

    public static string CartButton(int count) => $"Cart ({count})";

    public static string InStock(int stock) => stock > 0 ? $"In stock: {stock}" : "Out of stock";

    public static string Subtotal(decimal amount) => "Subtotal: " + Money.Format(amount);

    public static string Total(decimal amount) => "Total: " + Money.Format(amount);

    public static string OrderPlaced(OrderResult order) => $"Order {order.OrderId} placed. Total: {Money.Format(order.Total)}";
}
