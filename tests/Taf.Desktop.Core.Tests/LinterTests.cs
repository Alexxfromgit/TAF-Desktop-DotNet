using Allure.Net.Commons.Attributes;
using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Lint;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Users;

namespace Taf.Desktop.Core.Tests;

public class LinterTests
{
    [SetUp]
    public void Config() => TestConfig.UseDefaults();

    [TearDown]
    public void RestoreClock() => QuarantinedAttribute.Today = () => DateOnly.FromDateTime(DateTime.Today);

    [Test]
    public void TestsNeedOwnerAndFeature() =>
        Violations(typeof(Bare)).Should().BeEquivalentTo(
            "Bare.Test: needs an owner: [AllureOwner(\"team\")] on the method or class",
            "Bare.Test: needs a feature: [AllureFeature(\"...\")] or [AllureStory(\"...\")] on the method or class");

    [Test]
    public void ClassLevelMetadataCounts() => Violations(typeof(Tagged)).Should().BeEmpty();

    [Test]
    public void QuarantineMustBeValidAndNear()
    {
        QuarantinedAttribute.Today = () => new DateOnly(2026, 10, 1);

        Violations(typeof(Quarantines)).Should().BeEquivalentTo(
            "Quarantines.TooLong: [Quarantined] until 2027-06-01 is more than 90 days ahead",
            "Quarantines.Invalid: [Quarantined] date '1.11.2026' is not yyyy-MM-dd");
    }

    [Test]
    public void WindowsNeedIdentifiers() =>
        Violations(typeof(NoIdentifierWindow), typeof(NotLocatedWindow)).Should().BeEquivalentTo(
            "NoIdentifierWindow: a window needs at least one [WindowIdentifier] element (proves it is open)",
            "NotLocatedWindow: a window needs a class-level [Locate]");

    [Test]
    public void WellFormedWindowsPass() => Violations(typeof(ShopWindow)).Should().BeEmpty();

    private static IEnumerable<string> Violations(params Type[] types) =>
        MetadataLinter.ForTypes(types).Check().Select(v => v.ToString());

    [Explicit("Sample for the linter tests")]
    private sealed class Bare
    {
        [Test]
        public void Test()
        {
        }
    }

    [Explicit("Sample for the linter tests")]
    [AllureOwner("shop-team")]
    [AllureFeature("Checkout")]
    [TestUser("standard")]
    private sealed class Tagged
    {
        [Test]
        public void Test()
        {
        }
    }

    [Explicit("Sample for the linter tests")]
    [AllureOwner("shop-team")]
    [AllureFeature("Checkout")]
    private sealed class Quarantines
    {
        [Test]
        [Quarantined("2027-06-01", "waits for a redesign")]
        public void TooLong()
        {
        }

        [Test]
        [Quarantined("1.11.2026", "flaky")]
        public void Invalid()
        {
        }
    }
}
