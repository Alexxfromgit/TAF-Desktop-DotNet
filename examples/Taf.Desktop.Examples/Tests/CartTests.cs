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

[AllureFeature("Cart and checkout")]
[AllureOwner("checkout-team")]
[TestUser("standard")]
[LoggedIn]
public class CartTests : ShopDeskTest
{
    [Test]
    public void AddingProductsUpdatesTheCartButton()
    {
        var main = On<MainWindow>().AddToCart("Canvas Backpack", 2).AddToCart("Travel Mug");

        Verify.Equal(main.CartCount, 3, "Products in the cart");
    }

    [Test]
    [CartContains("Canvas Backpack", 2, Order = 1)]
    public void CartShowsLinesAndSubtotal()
    {
        var cart = On<MainWindow>().OpenCart();

        cart.Lines.Should().Equal("Canvas Backpack x2 - $59.98");
        Verify.Equal(cart.Subtotal, "Subtotal: $59.98", "Subtotal");
    }

    [Test]
    [CartContains("Canvas Backpack", 2, Order = 1)]
    public void ExpressShippingAndGiftWrapAreAddedToTheTotal()
    {
        var cart = On<MainWindow>().OpenCart().ChooseShipping("Express").GiftWrap(true);

        cart.WaitTotal("Total: $74.96");
    }

    [Test]
    [CartContains("Travel Mug", Order = 1)]
    public void OrderNeedsNameAndAddress()
    {
        var cart = On<MainWindow>().OpenCart();
        Verify.That(!cart.CanPlaceOrder, "Place order is disabled without customer data");

        cart.EnterCustomer("Ada Tester", "1 Test Street");

        Verify.That(cart.CanPlaceOrder, "Place order is enabled");
    }

    [Test]
    [Category(TestCategories.Smoke)]
    [Category(TestCategories.Blocker)]
    [CartContains("Canvas Backpack", 2, Order = 1)]
    public void PlacedOrderReachesTheBackend()
    {
        var main = On<MainWindow>();
        var confirmation = main.OpenCart().EnterCustomer("Ada Tester", "1 Test Street").ChooseShipping("Express").PlaceOrder();

        Verify.Equal(confirmation.Message, $"Order {ShopBackend.OrderId} placed. Total: $69.97", "Confirmation");
        confirmation.Ok();
        Verify.Equal(main.CartCount, 0, "Products in the cart after the order");

        var order = Backend.SingleRequest("POST", "/api/orders").Json;
        order.GetProperty("shipping").GetString().Should().Be("Express");
        order.GetProperty("customer").GetProperty("name").GetString().Should().Be("Ada Tester");
        order.GetProperty("items")[0].GetProperty("productId").GetInt32().Should().Be(1);
        order.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(2);
    }

    [Test]
    [CartContains("Canvas Backpack", Order = 1)]
    public void RemovingTheLastLineEmptiesTheCart()
    {
        var cart = On<MainWindow>().OpenCart().Remove("Canvas Backpack");

        Verify.That(cart.IsEmpty, "Empty-cart message is shown");
        cart.Lines.Should().BeEmpty();
    }

    [Test]
    [CartContains("Cotton T-Shirt", Order = 1)]
    public void RejectedOrderIsReported()
    {
        Backend.StubJson("POST", "/api/orders", new { error = "out of stock" }, status: 500, priority: 1);

        var message = On<MainWindow>().OpenCart().EnterCustomer("Ada Tester", "1 Test Street").PlaceOrder();

        Verify.Equal(message.Title, "Order failed", "Message title");
        message.Message.Should().Contain("HTTP 500");
        message.Ok();
    }
}
