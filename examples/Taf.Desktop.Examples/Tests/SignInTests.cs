using Allure.Net.Commons.Attributes;
using NUnit.Framework;
using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Users;
using Taf.Desktop.Core.Verification;
using Taf.Desktop.Examples.Support;
using Taf.Desktop.Examples.Windows;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Taf.Desktop.Examples.Tests;

[AllureFeature("Sign in")]
[AllureOwner("identity-team")]
public class SignInTests : ShopDeskTest
{
    [Test]
    [Category(TestCategories.Smoke)]
    [Category(TestCategories.Critical)]
    [TestUser("standard")]
    public void StandardUserSignsIn()
    {
        var main = On<LoginWindow>().SignInAs(User);

        Verify.Equal(main.Welcome, "Signed in as Standard User", "Welcome text");
    }

    [Test]
    [TestUser("standard")]
    public void WrongPasswordIsRejected()
    {
        var login = On<LoginWindow>().SignInExpectingError(User.Username, "not-the-password");

        Verify.Equal(login.Error, "Wrong username or password", "Error message");
        Backend.Verify("POST", "/api/login", 1);
    }

    [Test]
    [TestUser("locked")]
    public void LockedUserSeesWhy()
    {
        var login = On<LoginWindow>().SignInExpectingError(User.Username, User.Password);

        Verify.Equal(login.Error, "This account is locked", "Error message");
    }

    [Test]
    [TestUser("standard")]
    public void RememberMeKeepsTheUserName()
    {
        var login = On<LoginWindow>().SignInAs(User, remember: true).SignOut();

        Verify.Equal(login.Username, User.Username, "Remembered user name");
        Verify.That(login.IsRememberMeChecked, "Remember me is still checked");
    }

    [Test]
    [ResetAppData]
    public void FreshInstallRemembersNobody() =>
        Verify.Equal(On<LoginWindow>().Username, "", "User name field");

    [Test]
    [TestUser("standard")]
    public void UnreachableServerIsExplained()
    {
        Backend.Stub(Request.Create().UsingPost().WithPath("/api/login"), Response.Create().WithFault(FaultType.EMPTY_RESPONSE), priority: 1);

        var login = On<LoginWindow>().SignInExpectingError(User.Username, User.Password);

        Verify.Equal(login.Error, "Cannot reach the ShopDesk server", "Error message");
    }
}
