namespace ShopDesk.WinForms;

internal sealed class MainForm : Form
{
    private readonly Label welcome = new Label { AutoSize = true, Anchor = AnchorStyles.Left }.Named("WelcomeText");
    private readonly Button cartButton = new Button { AutoSize = true, Anchor = AnchorStyles.Right }.Named("CartButton");
    private readonly TextBox search = new TextBox { Width = 300 }.Named("SearchBox");
    private readonly Button searchButton = new Button { Text = "Search", AutoSize = true }.Named("SearchButton");
    private readonly ProgressBar loading = new ProgressBar { Style = ProgressBarStyle.Marquee, Height = 6, Dock = DockStyle.Top, Visible = false }.Named("LoadingIndicator");
    private readonly Label errorBanner = new Label { ForeColor = Color.Firebrick, AutoSize = true, Visible = false }.Named("ErrorBanner");
    private readonly Button retry = new Button { Text = "Retry", AutoSize = true, Visible = false }.Named("RetryButton");
    private readonly ListBox products = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Products" }.Named("ProductList");
    private readonly GroupBox details = new GroupBox { Text = "Details", Dock = DockStyle.Right, Width = 280 }.Named("DetailsPanel");
    private readonly Label detailsName = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), MaximumSize = new Size(240, 0) }.Named("DetailsName");
    private readonly Label detailsPrice = new Label { AutoSize = true }.Named("DetailsPrice");
    private readonly Label detailsStock = new Label { AutoSize = true }.Named("DetailsStock");
    private readonly TextBox quantity = new TextBox { Text = "1", Width = 60 }.Named("QuantityBox");
    private readonly Button addToCart = new Button { Text = "Add to cart", AutoSize = true, Enabled = false }.Named("AddToCartButton");
    private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(8, 4, 0, 0) }.Named("StatusText");

    public MainForm()
    {
        Name = "MainWindow";
        Text = AppText.MainTitle;
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        AcceptButton = searchButton;

        var menu = new MenuStrip { Name = "MainMenu" };
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(new ToolStripMenuItem("&Export cart...", null, (_, _) => Export()));
        file.DropDownItems.Add(new ToolStripMenuItem("&Sign out", null, (_, _) => SignOut()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (_, _) => Application.Exit()));
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(new ToolStripMenuItem("&About ShopDesk", null,
            (_, _) => MessageBox.Show(this, AppText.AboutText, AppText.AboutTitle)));
        menu.Items.AddRange([file, help]);


        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(welcome, 0, 0);
        header.Controls.Add(cartButton, 1, 0);

        var searchRow = Ui.Row(new Label { Text = "Search", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, search, searchButton);
        searchRow.Dock = DockStyle.Top;
        var errorRow = Ui.Row(errorBanner, retry);
        errorRow.Dock = DockStyle.Top;

        details.Controls.Add(Ui.Column(detailsName, detailsPrice, detailsStock, Ui.Caption("Quantity"), quantity, addToCart));

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        content.Controls.Add(products);
        content.Controls.Add(details);
        content.Controls.Add(errorRow);
        content.Controls.Add(loading);
        content.Controls.Add(searchRow);
        content.Controls.Add(header);

        Controls.Add(content);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        welcome.Text = AppText.Welcome(Services.DisplayName);
        Services.Cart.Changed += OnCartChanged;
        FormClosed += (_, _) => Services.Cart.Changed -= OnCartChanged;
        OnCartChanged(null, EventArgs.Empty);

        searchButton.Click += async (_, _) => await LoadProductsAsync();
        retry.Click += async (_, _) => await LoadProductsAsync();
        products.SelectedIndexChanged += (_, _) => OnProductSelected();
        addToCart.Click += (_, _) => AddToCart();
        cartButton.Click += (_, _) =>
        {
            using var cart = new CartForm();
            cart.ShowDialog(this);
        };
        Shown += async (_, _) => await LoadProductsAsync();
    }

    private Product? Selected => products.SelectedItem as Product;

    private async Task LoadProductsAsync()
    {
        errorBanner.Visible = false;
        retry.Visible = false;
        loading.Visible = true;
        status.Text = "Loading products...";
        searchButton.Enabled = false;
        try
        {
            var list = await Services.Api.GetProductsAsync(search.Text);
            products.Items.Clear();
            products.Items.AddRange(list.Cast<object>().ToArray());
            products.ClearSelected();
            OnProductSelected();
            status.Text = list.Count == 0 ? AppText.NoProducts : AppText.ProductCount(list.Count);
        }
        catch (ShopApiException ex)
        {
            products.Items.Clear();
            errorBanner.Text = ex.Message;
            errorBanner.Visible = true;
            retry.Visible = true;
            status.Text = "";
        }
        finally
        {
            loading.Visible = false;
            searchButton.Enabled = true;
        }
    }

    private void OnProductSelected()
    {
        var product = Selected;
        detailsName.Text = product?.Name ?? "";
        detailsPrice.Text = product is null ? "" : Money.Format(product.Price);
        detailsStock.Text = product is null ? "" : AppText.InStock(product.Stock);
        quantity.Text = "1";
        addToCart.Enabled = product is { Stock: > 0 };
    }

    private void AddToCart()
    {
        if (Selected is { } product && int.TryParse(quantity.Text, out var count) && count > 0)
        {
            Services.Cart.Add(product, count);
            status.Text = $"Added {product.Name} to the cart";
        }
        else
        {
            status.Text = "Enter a quantity of 1 or more";
        }
    }

    private void OnCartChanged(object? sender, EventArgs e) => cartButton.Text = AppText.CartButton(Services.Cart.Count);

    private void Export()
    {
        if (Services.Cart.Count == 0)
        {
            MessageBox.Show(this, AppText.EmptyCart, AppText.ExportTitle);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = AppText.ExportTitle,
            FileName = "cart.csv",
            DefaultExt = "csv",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, Services.Cart.ToCsv());
            status.Text = "Cart exported";
        }
    }

    private void SignOut()
    {
        Services.Api.SignOut();
        Services.Cart.Clear();
        Services.Context.Show(new LoginForm());
    }
}
