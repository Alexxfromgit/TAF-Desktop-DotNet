using System.Globalization;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Testing;

/// <summary>
/// Marks a test that fails because of an already reported bug. It still runs; a failure is reported under
/// "Known issues" with a link to the issue, and a pass logs a warning to remove the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class KnownIssueAttribute(string id) : NUnitAttribute, IWrapTestMethod
{
    public string Id { get; } = id;

    /// <summary>Full URL of the issue; by default the <c>{issue}</c> link pattern of <c>allureConfig.json</c> is used.</summary>
    public string? Url { get; init; }

    public TestCommand Wrap(TestCommand command) => new KnownIssueCommand(command, Id);

    /// <summary>NUnit and reflection wrap the test's exception; the report should show the original failure.</summary>
    private static Exception Unwrap(Exception e)
    {
        while (e is System.Reflection.TargetInvocationException or NUnitException && e.InnerException is { } inner)
        {
            e = inner;
        }
        return e;
    }

    private sealed class KnownIssueCommand(TestCommand inner, string id) : DelegatingTestCommand(inner)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            try
            {
                return innerCommand.Execute(context);
            }
            catch (Exception e) when (e is not KnownIssueException && e is not ResultStateException { ResultState.Status: not TestStatus.Failed })
            {
                throw new KnownIssueException(id, Unwrap(e));
            }
        }
    }
}

/// <summary>
/// Skips a flaky or broken test until <paramref name="until"/> (yyyy-MM-dd); from that day on it runs again
/// automatically. <see cref="TafTest"/> skips it at set-up, so it is reported as skipped with its reason. The linter
/// allows at most <c>Lint:QuarantineMaxDays</c> (90) days; an invalid date makes the test not runnable.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class QuarantinedAttribute(string until, string reason) : NUnitAttribute, IApplyToTest
{
    internal static Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    public string UntilText { get; } = until;

    public string Reason { get; } = reason;

    public DateOnly? Until => DateOnly.TryParseExact(UntilText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        ? date
        : null;

    public bool IsActive => Until is { } date && Today() < date;

    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable)
        {
            return;
        }
        if (Until is null)
        {
            test.RunState = RunState.NotRunnable;
            test.Properties.Set(PropertyNames.SkipReason, $"[Quarantined] date '{UntilText}' is not yyyy-MM-dd");
        }
    }
}

/// <summary>
/// Retries the test when it failed because of the infrastructure (app did not start, UI Automation timeout,
/// unreachable backend) - never because of an assertion.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class InfraRetryAttribute(int attempts = 2) : NUnitAttribute, IRepeatTest
{
    private static readonly string[] InfrastructureTypes =
    [
        nameof(EnvironmentException), nameof(TimeoutException), "COMException", "Win32Exception", "ElementNotAvailableException",
        "HttpRequestException", "SocketException",
    ];

    public int Attempts { get; } = attempts;

    public TestCommand Wrap(TestCommand command) => new RetryCommand(command, Attempts);

    /// <summary>True if the failure message or trace names an infrastructure exception.</summary>
    public static bool IsInfrastructureFailure(string? message, string? trace)
    {
        var text = (message ?? "") + "\n" + (trace ?? "");
        return InfrastructureTypes.Any(t => text.Contains(t, StringComparison.Ordinal));
    }

    private sealed class RetryCommand(TestCommand inner, int attempts) : DelegatingTestCommand(inner)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            for (var attempt = 1; ; attempt++)
            {
                context.CurrentResult = innerCommand.Execute(context);
                var result = context.CurrentResult;
                if (attempt >= attempts || result.ResultState.Status != TestStatus.Failed
                                        || !IsInfrastructureFailure(result.Message, result.StackTrace))
                {
                    return result;
                }
                TestContext.Progress.WriteLine($"Retrying {context.CurrentTest.FullName} after an infrastructure failure "
                                               + $"(attempt {attempt + 1} of {attempts}): {result.Message?.Split('\n')[0]}");
                context.CurrentResult = context.CurrentTest.MakeTestResult();
                context.CurrentRepeatCount++;
            }
        }
    }
}
