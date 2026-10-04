using Allure.Net.Commons.Attributes;
using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Users;
using Taf.Desktop.Core.Verification;
using Taf.Desktop.Examples.Backend;
using Taf.Desktop.Examples.Support;
using Taf.Desktop.Examples.Windows;

namespace Taf.Desktop.Examples.Tests;

[AllureFeature("Catalog")]
[AllureOwner("catalog-team")]
[TestUser("standard")]
[LoggedIn]
public class CatalogTests : ShopDeskTest
{
    [Test]
    [Category(TestCategories.Smoke)]
    public void CatalogShowsTheProductsOfTheBackend()
    {
        var main = On<MainWindow>();

        main.ProductNames.Should().Equal(ShopBackend.Catalog.Select(p => p.Name));
        Verify.Equal(main.Status, "6 products", "Status bar");
    }

    [Test]
    public void SearchSendsTheQueryAndShowsTheMatches()
    {
        var main = On<MainWindow>().Search("shirt");

        main.ProductNames.Should().Equal("Cotton T-Shirt");
        Backend.Requests("GET", "/api/products").Last().Query["search"].Should().Be("shirt");
    }

    [Test]
    public void SearchWithoutMatchesSaysSo() =>
        Verify.Equal(On<MainWindow>().Search("umbrella").Status, "No products found", "Status bar");

    [Test]
    public void DetailsShowPriceAndStock()
    {
        var details = On<MainWindow>().Select("Fleece Jacket");

        Verify.Equal(details.Price, "$49.99", "Price");
        Verify.Equal(details.Stock, "In stock: 3", "Stock");
    }

    [Test]
    public void OutOfStockProductsCannotBeAdded()
    {
        var details = On<MainWindow>().Select("Bike Light");

        Verify.Equal(details.Stock, "Out of stock", "Stock");
        Verify.That(!details.CanAddToCart, "Add to cart is disabled");
    }

    [Test]
    public void BackendErrorOffersARetry()
    {
        Backend.StubJson("GET", "/api/products", new { error = "boom" }, status: 500, priority: 1);
        var main = On<MainWindow>().Search("");

        Verify.Equal(main.Error, "Could not load products (HTTP 500)", "Error banner");

        Backend.Reset();
        main.Retry();
        Verify.Equal(main.Status, "6 products", "Status bar after retry");
    }

    [Test]
    public void SlowBackendShowsALoadingIndicator()
    {
        Backend.StubJson("GET", "/api/products", ShopBackend.Catalog.Take(2), delay: TimeSpan.FromSeconds(2), priority: 1);
        var main = On<MainWindow>().StartSearch("");

        Verify.That(main.IsLoading, "Loading indicator is visible while the backend answers");
        main.WaitUntilLoaded();
        Verify.Equal(main.Status, "2 products", "Status bar");
    }
}
