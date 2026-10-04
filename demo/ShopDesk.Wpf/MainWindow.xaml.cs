using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ShopDesk.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = AppText.Welcome(Services.DisplayName);
        Services.Cart.Changed += OnCartChanged;
        Closed += (_, _) => Services.Cart.Changed -= OnCartChanged;
        OnCartChanged(null, EventArgs.Empty);
        Loaded += async (_, _) => await LoadProductsAsync();
    }

    private Product? Selected => ProductList.SelectedItem as Product;

    private async Task LoadProductsAsync()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        LoadingIndicator.Visibility = Visibility.Visible;
        StatusText.Text = "Loading products...";
        SearchButton.IsEnabled = false;
        try
        {
            var products = await Services.Api.GetProductsAsync(SearchBox.Text);
            ProductList.ItemsSource = products;
            StatusText.Text = products.Count == 0 ? AppText.NoProducts : AppText.ProductCount(products.Count);
        }
        catch (ShopApiException ex)
        {
            ProductList.ItemsSource = Array.Empty<Product>();
            ErrorBanner.Text = ex.Message;
            ErrorBanner.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Visible;
            StatusText.Text = "";
        }
        finally
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            SearchButton.IsEnabled = true;
        }
    }

    private async void OnSearch(object sender, RoutedEventArgs e) => await LoadProductsAsync();

    private async void OnRetry(object sender, RoutedEventArgs e) => await LoadProductsAsync();

    private void OnProductSelected(object sender, RoutedEventArgs e)
    {
        var product = Selected;
        DetailsName.Text = product?.Name ?? "";
        DetailsPrice.Text = product is null ? "" : Money.Format(product.Price);
        DetailsStock.Text = product is null ? "" : AppText.InStock(product.Stock);
        QuantityBox.Text = "1";
        AddToCartButton.IsEnabled = product is { Stock: > 0 };
    }

    private void OnAddToCart(object sender, RoutedEventArgs e)
    {
        if (Selected is { } product && int.TryParse(QuantityBox.Text, out var quantity) && quantity > 0)
        {
            Services.Cart.Add(product, quantity);
            StatusText.Text = $"Added {product.Name} to the cart";
        }
        else
        {
            StatusText.Text = "Enter a quantity of 1 or more";
        }
    }

    private void OnCartChanged(object? sender, EventArgs e) => CartButton.Content = AppText.CartButton(Services.Cart.Count);

    private void OnOpenCart(object sender, RoutedEventArgs e) => new CartWindow { Owner = this }.ShowDialog();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (Services.Cart.Count == 0)
        {
            MessageBox.Show(this, AppText.EmptyCart, AppText.ExportTitle);
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = AppText.ExportTitle,
            FileName = "cart.csv",
            DefaultExt = ".csv",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, Services.Cart.ToCsv());
            StatusText.Text = AppText.Exported(dialog.FileName);
        }
    }

    private void OnSignOut(object sender, RoutedEventArgs e)
    {
        Services.Api.SignOut();
        Services.Cart.Clear();
        new LoginWindow().Show();
        Close();
    }

    private void OnExit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnAbout(object sender, RoutedEventArgs e) => MessageBox.Show(this, AppText.AboutText, AppText.AboutTitle);
}
