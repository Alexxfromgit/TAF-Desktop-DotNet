using System.Globalization;

namespace ShopDesk;

public sealed record Product(int Id, string Name, decimal Price, int Stock)
{
    /// <summary>The text of a row in the product list: <c>Canvas Backpack - $29.99</c>.</summary>
    public override string ToString() => $"{Name} - {Money.Format(Price)}";
}

public sealed record CartLine(Product Product, int Quantity)
{
    public decimal Amount => Product.Price * Quantity;

    public override string ToString() => $"{Product.Name} x{Quantity} - {Money.Format(Amount)}";
}

public enum Shipping
{
    Standard,
    Express,
}

public sealed record Customer(string Name, string Address);

public sealed record LoginResult(bool Success, string? DisplayName, string? Token, string? Error);

public sealed record OrderResult(string OrderId, decimal Total);

public static class Money
{
    public static string Format(decimal amount) => "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
