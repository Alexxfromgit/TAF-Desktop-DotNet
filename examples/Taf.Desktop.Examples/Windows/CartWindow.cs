using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Examples.Windows;

[Locate(AutomationId = "CartWindow")]
public sealed class CartWindow : Window
{
    [WindowIdentifier]
    [Locate(AutomationId = "TotalText")]
    private UiElement total = null!;

    [Locate(AutomationId = "EmptyCartText")]
    private UiElement emptyCart = null!;

    [Locate(AutomationId = "RemoveButton")]
    private UiElement remove = null!;

    [Locate(AutomationId = "SubtotalText")]
    private UiElement subtotal = null!;

    [Locate(AutomationId = "ShippingCombo")]
    private UiElement shipping = null!;

    [Locate(AutomationId = "GiftWrapCheckBox")]
    private UiElement giftWrap = null!;

    [Locate(AutomationId = "CustomerNameBox")]
    private UiElement customerName = null!;

    [Locate(AutomationId = "AddressBox")]
    private UiElement address = null!;

    [Locate(AutomationId = "PlaceOrderButton")]
    private UiElement placeOrder = null!;

    [Locate(AutomationId = "CloseButton")]
    private UiElement close = null!;

    private CartLines lines = null!;

    public IReadOnlyList<string> Lines => lines.Texts;

    public bool IsEmpty => emptyCart.IsDisplayed;

    public string Subtotal => subtotal.Text;

    public string Total => total.Text;

    public bool CanPlaceOrder => placeOrder.IsEnabled;

    public CartWindow Remove(string product)
    {
        lines.Line(product).Root.Select();
        remove.Click();
        return this;
    }

    public CartWindow ChooseShipping(string option)
    {
        shipping.Select(option);
        return this;
    }

    public CartWindow GiftWrap(bool wrap)
    {
        giftWrap.SetChecked(wrap);
        return this;
    }

    public CartWindow EnterCustomer(string name, string street)
    {
        customerName.Type(name);
        address.Type(street);
        return this;
    }

    public CartWindow WaitTotal(string expected)
    {
        total.WaitText(expected);
        return this;
    }

    /// <summary>Places the order; the app answers with a message box.</summary>
    public MessageBoxDialog PlaceOrder() => placeOrder.ClickAndExpect<MessageBoxDialog>();

    public void CloseCart()
    {
        close.Click();
        WaitClosed();
    }
}

[Locate(AutomationId = "CartItems")]
public sealed class CartLines : Component
{
    [Locate(ControlType = FlaUI.Core.Definitions.ControlType.ListItem)]
    private ComponentList<ProductRow> rows = null!;

    public IReadOnlyList<string> Texts => rows.All().Select(r => r.Text).ToList();

    public ProductRow Line(string product) => rows.First(r => r.Text.StartsWith(product + " x", StringComparison.Ordinal), $"for '{product}'");
}
