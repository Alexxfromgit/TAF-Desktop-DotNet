using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Users;
using Taf.Desktop.Core.Verification;

namespace Taf.Desktop.Core.Tests;

public class VerifyAndUsersTests
{
    [TearDown]
    public void Restore() => TestConfig.UseDefaults();

    [Test]
    public void HardVerifyFailsWithExpectedAndActual()
    {
        TestConfig.UseDefaults();

        FluentActions.Invoking(() => Verify.Equal("$59.98", "$64.97", "Order total"))
            .Should().Throw<PotentialDefectException>().WithMessage("Order total: expected '$64.97' but was '$59.98'");
    }

    [Test]
    public void PassingVerifyDoesNothing()
    {
        TestConfig.UseDefaults();

        Verify.That(true, "Cart is empty");
        Verify.Equal(3, 3, "Product count");
    }

    [Test]
    public void UsersCombineUsernameAndSecretPassword()
    {
        TafConfig.Use(TestConfig.Load(new Dictionary<string, string?> { ["TAF__Users__standard__Password"] = "s3cret" }));

        var user = TestUsers.Get("standard");

        user.Should().Be(new UserCredentials("standard", "standard_user", "s3cret"));
        user.ToString().Should().Be("standard (standard_user, password ****)");
    }
}
