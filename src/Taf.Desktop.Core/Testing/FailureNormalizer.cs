using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Testing;

/// <summary>
/// Prepares a failed result for the report before Allure reads it:
/// <list type="bullet">
/// <item>assertion failures that NUnit did not record as assertions (FluentAssertions, framework exceptions) are
/// recorded, so Allure reports them as "failed" instead of "broken";</item>
/// <item>the exception type is put on the first line of the trace, where the Allure categories match it.</item>
/// </list>
/// </summary>
internal static class FailureNormalizer
{
    public static void Normalize()
    {
        var context = TestExecutionContext.CurrentContext;
        var result = context.CurrentResult;
        var registered = FailureRegistry.Take(context.CurrentTest.Id);
        if (result.ResultState.Status != TestStatus.Failed)
        {
            return;
        }
        var message = result.Message ?? "";
        var isError = result.ResultState.Label == "Error";
        var typeLine = registered != null
            ? $"{registered.GetType().FullName}: {registered.Message}"
            : isError ? ErrorTypeLine(message) : null;
        var trace = result.StackTrace ?? "";
        if (typeLine != null && !trace.StartsWith(typeLine, StringComparison.Ordinal))
        {
            trace = typeLine + Environment.NewLine + trace;
            result.SetResult(result.ResultState, message, trace);
        }
        if (!isError && result.AssertionResults.Count == 0)
        {
            result.RecordAssertion(AssertionStatus.Failed, message, trace);
        }
    }

    /// <summary>NUnit reports exceptions as <c>Namespace.Type : message</c>.</summary>
    private static string? ErrorTypeLine(string message)
    {
        var firstLine = message.Split('\n')[0].TrimEnd('\r');
        var separator = firstLine.IndexOf(" : ", StringComparison.Ordinal);
        return separator > 0 ? firstLine[..separator] + ": " + firstLine[(separator + 3)..] : null;
    }
}
