using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Evidence;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Verification;

public enum VerifyMode
{
    Hard,
    Soft,
}

/// <summary>
/// Readable checks that become report steps. In <c>Verify:Mode=Hard</c> the first failure stops the test; in
/// <c>Soft</c> all failures of a test are collected (with a screenshot each) and the test fails at the end.
/// </summary>
public static class Verify
{
    public static bool IsSoftMode => TafConfig.Current.Enum("Verify:Mode", VerifyMode.Hard) == VerifyMode.Soft;

    public static void That(bool condition, string description) =>
        Check(condition, description, $"{description}: not true");

    public static void Equal<T>(T actual, T expected, string description) =>
        Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{description} is {Format(expected)}",
            $"{description}: expected {Format(expected)} but was {Format(actual)}");

    /// <summary>
    /// Runs assertions of any library (FluentAssertions, NUnit) softly: failures are collected instead of stopping the
    /// test, whatever the mode.
    /// </summary>
    public static void Softly(string description, Action assertions)
    {
        try
        {
            Step.Run(description, assertions);
        }
        catch (AssertionException e)
        {
            RecordSoftFailure(e.Message);
        }
    }

    private static void Check(bool ok, string step, string failure)
    {
        Step.Report(step, ok);
        if (ok)
        {
            return;
        }
        if (IsSoftMode)
        {
            RecordSoftFailure(failure);
        }
        else
        {
            throw new PotentialDefectException(failure);
        }
    }

    private static void RecordSoftFailure(string message)
    {
        // A recorded assertion failure lets the test continue; NUnit fails it when the test method returns.
        TestExecutionContext.CurrentContext.CurrentResult.RecordAssertion(AssertionStatus.Failed, message, Environment.StackTrace);
        if (TestScope.TryCurrent is { } scope)
        {
            scope.SoftFailures++;
            FailureEvidence.OnSoftFailure(scope.SoftFailures);
        }
    }

    private static string Format<T>(T value) => value is string s ? $"'{s}'" : value?.ToString() ?? "null";
}
