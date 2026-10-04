namespace ShopDesk.WinForms;

internal sealed class CartForm : Form
{
    private readonly Label emptyCart = new Label { Text = AppText.EmptyCart, AutoSize = true, Visible = false }.Named("EmptyCartText");
    private readonly ListBox items = new ListBox { Width = 430, Height = 140, AccessibleName = "Cart items" }.Named("CartItems");
    private readonly Button remove = new Button { Text = "Remove selected", AutoSize = true }.Named("RemoveButton");
    private readonly Label subtotal = new Label { AutoSize = true }.Named("SubtotalText");
    private readonly ComboBox shipping = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 }.Named("ShippingCombo");
    private readonly CheckBox giftWrap = new CheckBox { Text = "Gift wrap (+$4.99)", AutoSize = true }.Named("GiftWrapCheckBox");
    private readonly Label total = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) }.Named("TotalText");
    private readonly TextBox customerName = new TextBox { Width = 430 }.Named("CustomerNameBox");
    private readonly TextBox address = new TextBox { Width = 430 }.Named("AddressBox");
    private readonly Button placeOrder = new Button { Text = "Place order", AutoSize = true, Enabled = false }.Named("PlaceOrderButton");
    private readonly Button close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel }.Named("CloseButton");

    public CartForm()
    {
        Name = "CartWindow";
        Text = AppText.CartTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        CancelButton = close;

        shipping.Items.AddRange(Enum.GetNames<Shipping>());
        shipping.SelectedIndex = 0;

        Controls.Add(Ui.Column(emptyCart, items, remove, subtotal, Ui.Caption("Shipping"), shipping, giftWrap, total,
            Ui.Caption("Name"), customerName, Ui.Caption("Address"), address, Ui.Row(placeOrder, close)));

        remove.Click += (_, _) => Remove();
        shipping.SelectedIndexChanged += (_, _) => UpdateView();
        giftWrap.CheckedChanged += (_, _) => UpdateView();
        customerName.TextChanged += (_, _) => UpdateView();
        address.TextChanged += (_, _) => UpdateView();
        placeOrder.Click += async (_, _) => await PlaceOrderAsync();
        UpdateView();
    }

    private Shipping SelectedShipping => shipping.SelectedIndex == 1 ? Shipping.Express : Shipping.Standard;

    private void UpdateView()
    {
        var cart = Services.Cart;
        items.Items.Clear();
        items.Items.AddRange(cart.Lines.Cast<object>().ToArray());
        emptyCart.Visible = cart.Count == 0;
        remove.Enabled = cart.Count > 0;
        subtotal.Text = AppText.Subtotal(cart.Subtotal);
        total.Text = AppText.Total(cart.Total(SelectedShipping, giftWrap.Checked));
        placeOrder.Enabled = cart.Count > 0
                             && !string.IsNullOrWhiteSpace(customerName.Text)
                             && !string.IsNullOrWhiteSpace(address.Text);
    }

    private void Remove()
    {
        if (items.SelectedItem is CartLine line)
        {
            Services.Cart.Remove(line);
            UpdateView();
        }
    }

    private async Task PlaceOrderAsync()
    {
        placeOrder.Enabled = false;
        try
        {
            var order = await Services.Api.PlaceOrderAsync(Services.Cart,
                new Customer(customerName.Text.Trim(), address.Text.Trim()), SelectedShipping, giftWrap.Checked);
            MessageBox.Show(this, AppText.OrderPlaced(order), AppText.OrderPlacedTitle);
            Services.Cart.Clear();
            DialogResult = DialogResult.OK;
        }
        catch (ShopApiException ex)
        {
            MessageBox.Show(this, "Could not place the order: " + ex.Message, AppText.OrderFailedTitle);
            UpdateView();
        }
    }
}
