using Taf.Desktop.Core.Preconditions;
using Taf.Desktop.Examples.Windows;

namespace Taf.Desktop.Examples.Support;

/// <summary>The test starts signed in as its [TestUser].</summary>
public sealed class LoggedInAttribute : PreconditionAttribute
{
    public override string Description => "signed in";

    public override void Establish(PreconditionContext context) => context.On<LoginWindow>().SignInAs(context.User);
}

/// <summary>The test starts with a product in the cart (needs [LoggedIn]).</summary>
public sealed class CartContainsAttribute(string product) : PreconditionAttribute
{
    public CartContainsAttribute(string product, int quantity) : this(product) => Quantity = quantity;

    public string Product { get; } = product;

    public int Quantity { get; } = 1;

    public override string Description => $"cart contains {Quantity} x {Product}";

    public override void Establish(PreconditionContext context) => context.On<MainWindow>().AddToCart(Product, Quantity);
}
