namespace ShopDesk;

/// <summary>Cart of the signed-in user. Prices: express shipping $9.99, gift wrap $4.99.</summary>
public sealed class Cart
{
    public const decimal ExpressShipping = 9.99m;
    public const decimal GiftWrap = 4.99m;

    private readonly List<CartLine> lines = [];

    public event EventHandler? Changed;

    public IReadOnlyList<CartLine> Lines => lines;

    public int Count => lines.Sum(l => l.Quantity);

    public decimal Subtotal => lines.Sum(l => l.Amount);

    public decimal Total(Shipping shipping, bool giftWrap) =>
        Subtotal + (shipping == Shipping.Express ? ExpressShipping : 0) + (giftWrap ? GiftWrap : 0);

    public void Add(Product product, int quantity)
    {
        var index = lines.FindIndex(l => l.Product.Id == product.Id);
        if (index >= 0)
        {
            lines[index] = lines[index] with { Quantity = lines[index].Quantity + quantity };
        }
        else
        {
            lines.Add(new CartLine(product, quantity));
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(CartLine line)
    {
        lines.Remove(line);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lines.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string ToCsv()
    {
        var rows = lines.Select(l => $"{Csv(l.Product.Name)},{l.Quantity},{l.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
        return string.Join("\n", new[] { "Product,Quantity,Amount" }.Concat(rows)) + "\n";
    }

    private static string Csv(string value) => value.Contains(',') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
