using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Testing;
using static Taf.Desktop.Core.App.StartMode;

namespace Taf.Desktop.Core.Tests;

public class AppLifecyclePolicyTests
{
    // FirstStart, AppRunning, PreviousFailed, FreshApp, ResetAppData, PreviousUser, NextUser -> expected
    [TestCase(true, false, false, false, false, null, "standard", ResetAppData, TestName = "first test resets leftovers of earlier runs")]
    [TestCase(false, false, false, false, false, null, null, RestartApp, TestName = "app not running is started again")]
    [TestCase(false, true, true, false, false, null, null, RestartApp, TestName = "after a failure the app restarts")]
    [TestCase(false, true, false, true, false, null, null, RestartApp, TestName = "FreshApp restarts even in Reuse mode")]
    [TestCase(false, true, false, false, true, null, null, ResetAppData, TestName = "ResetAppData wipes data")]
    [TestCase(false, true, false, false, false, "standard", "locked", ResetAppData, TestName = "another user resets app data")]
    [TestCase(false, true, false, false, false, "standard", "standard", Reuse, TestName = "same user keeps the default mode")]
    public void DecidesTheCheapestSafeStart(bool first, bool running, bool failed, bool fresh, bool reset, string? previous, string? next,
        StartMode expected)
    {
        var situation = new AppLifecyclePolicy.Situation(first, running, failed, fresh, reset, previous, next);

        AppLifecyclePolicy.Decide(situation, Reuse, RestartApp, resetDataOnFirstStart: true).Should().Be(expected);
    }

    [Test]
    public void FirstStartCanKeepData() =>
        AppLifecyclePolicy.Decide(new AppLifecyclePolicy.Situation(true, false, false, false, false, null, null), Reuse, RestartApp, false)
            .Should().Be(RestartApp);
}

public class QuarantineAndRetryTests
{
    [TearDown]
    public void RestoreClock() => QuarantinedAttribute.Today = () => DateOnly.FromDateTime(DateTime.Today);

    [Test]
    public void QuarantineIsActiveUntilItsDate()
    {
        var quarantine = new QuarantinedAttribute("2026-11-15", "flaky on CI");

        QuarantinedAttribute.Today = () => new DateOnly(2026, 11, 14);
        quarantine.IsActive.Should().BeTrue();
        QuarantinedAttribute.Today = () => new DateOnly(2026, 11, 15);
        quarantine.IsActive.Should().BeFalse();
    }

    [Test]
    public void ValidQuarantineKeepsTheTestRunnable()
    {
        var test = TestFor(nameof(Sample.Flaky));

        new QuarantinedAttribute("2026-11-15", "flaky on CI").ApplyToTest(test);

        test.RunState.Should().Be(RunState.Runnable);
    }

    [Test]
    public void InvalidQuarantineDateMakesTheTestNotRunnable()
    {
        var test = TestFor(nameof(Sample.Flaky));

        new QuarantinedAttribute("15.11.2026", "flaky").ApplyToTest(test);

        test.RunState.Should().Be(RunState.NotRunnable);
    }

    [TestCase("Taf.Desktop.Core.Failures.EnvironmentException : app did not start", true)]
    [TestCase("System.TimeoutException : UIA Timeout", true)]
    [TestCase("Expected total to be $10.00", false)]
    [TestCase("ShopWindow.signIn is not visible after 10s", false)]
    public void RetriesOnlyInfrastructureFailures(string message, bool retried) =>
        InfraRetryAttribute.IsInfrastructureFailure(message, "").Should().Be(retried);

    private static TestMethod TestFor(string method) => new(new MethodWrapper(typeof(Sample), method));

    [Explicit("Sample for the quarantine tests")]
    private sealed class Sample
    {
        public void Flaky()
        {
        }
    }
}
