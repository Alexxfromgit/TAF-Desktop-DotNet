using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Ui;
using Taf.Desktop.Core.Users;

namespace Taf.Desktop.Core.Preconditions;

/// <summary>
/// A declarative precondition: derive an attribute, put it on tests, and the framework establishes it after the app
/// started and before the test body (in <see cref="Order"/>, class attributes first). Example: <c>[LoggedIn]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public abstract class PreconditionAttribute : Attribute
{
    public int Order { get; init; }

    /// <summary>Shown as the report step: <c>Precondition: logged in</c>.</summary>
    public abstract string Description { get; }

    public abstract void Establish(PreconditionContext context);
}

/// <summary>What a precondition can use: the app, the test's user and windows.</summary>
public sealed class PreconditionContext(TestScope scope)
{
    public TestScope Scope { get; } = scope;

    public AppSession App => AppSession.Current!;

    public UserCredentials User => Scope.User;

    public T On<T>() where T : Window => WindowFactory.On<T>();
}
