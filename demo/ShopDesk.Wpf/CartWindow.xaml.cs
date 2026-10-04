using System.Windows;

namespace ShopDesk.Wpf;

public partial class CartWindow : Window
{
    public CartWindow()
    {
        InitializeComponent();
        ShippingCombo.ItemsSource = Enum.GetNames<Shipping>();
        ShippingCombo.SelectedIndex = 0;
        Refresh();
    }

    private Shipping Shipping => ShippingCombo.SelectedIndex == 1 ? Shipping.Express : Shipping.Standard;

    private void Refresh()
    {
        var cart = Services.Cart;
        CartItems.ItemsSource = cart.Lines.ToList();
        EmptyCartText.Visibility = cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.IsEnabled = cart.Count > 0;
        SubtotalText.Text = AppText.Subtotal(cart.Subtotal);
        TotalText.Text = AppText.Total(cart.Total(Shipping, GiftWrapCheckBox.IsChecked == true));
        PlaceOrderButton.IsEnabled = cart.Count > 0
                                     && !string.IsNullOrWhiteSpace(CustomerNameBox.Text)
                                     && !string.IsNullOrWhiteSpace(AddressBox.Text);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (CartItems.SelectedItem is CartLine line)
        {
            Services.Cart.Remove(line);
            Refresh();
        }
    }

    private void OnOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            Refresh();
        }
    }

    private void OnFormChanged(object sender, RoutedEventArgs e) => OnOptionsChanged(sender, e);

    private async void OnPlaceOrder(object sender, RoutedEventArgs e)
    {
        PlaceOrderButton.IsEnabled = false;
        try
        {
            var order = await Services.Api.PlaceOrderAsync(Services.Cart,
                new Customer(CustomerNameBox.Text.Trim(), AddressBox.Text.Trim()), Shipping, GiftWrapCheckBox.IsChecked == true);
            MessageBox.Show(this, AppText.OrderPlaced(order), AppText.OrderPlacedTitle);
            Services.Cart.Clear();
            DialogResult = true;
        }
        catch (ShopApiException ex)
        {
            MessageBox.Show(this, "Could not place the order: " + ex.Message, AppText.OrderFailedTitle);
            Refresh();
        }
    }
}
