using System.Collections.Concurrent;
using NUnit.Framework;

namespace Taf.Desktop.Core.Failures;

// Failure taxonomy. Subclasses of AssertionException are reported as "failed" (the product or the UI differs from
// the expectation), the others as "broken" (the test could not run properly). Allure categories group them by type.

/// <summary>The product behaves differently from what the test expects.</summary>
public class PotentialDefectException : AssertionException
{
    public PotentialDefectException(string message) : base(message) => FailureRegistry.Record(this);

    public PotentialDefectException(string message, Exception inner) : base(message, inner) => FailureRegistry.Record(this);
}

/// <summary>An element did not appear in time: the UI changed or a locator is outdated.</summary>
public sealed class ElementNotFoundException : AssertionException
{
    public ElementNotFoundException(string message) : base(message) => FailureRegistry.Record(this);
}

/// <summary>A window did not become ready (identifiers visible, loading indicators gone) in time.</summary>
public sealed class WindowNotReadyException : AssertionException
{
    public WindowNotReadyException(string message, Exception? inner = null) : base(message, inner!) => FailureRegistry.Record(this);
}

/// <summary>A failure of a test marked with [KnownIssue]: an already reported bug, not a new one.</summary>
public sealed class KnownIssueException : AssertionException
{
    public KnownIssueException(string issueId, Exception inner) : base($"[Known issue {issueId}] {inner.Message}", inner)
    {
        IssueId = issueId;
        FailureRegistry.Record(this);
    }

    public string IssueId { get; }
}

/// <summary>Base of the "broken" failures raised by the framework.</summary>
public abstract class TafException : Exception
{
    protected TafException(string message, Exception? inner = null) : base(message, inner) => FailureRegistry.Record(this);
}

/// <summary>The test could not find or create the data it needs.</summary>
public class TestDataException(string message, Exception? inner = null) : TafException(message, inner);

/// <summary>The environment is not usable: the app did not start, a backend is unreachable, a secret is missing.</summary>
public class EnvironmentException(string message, Exception? inner = null) : TafException(message, inner);

/// <summary>Misconfiguration or a bug in the framework or the test code.</summary>
public class FrameworkException(string message, Exception? inner = null) : TafException(message, inner);

/// <summary>
/// Remembers the last framework failure of each test. NUnit only keeps the message of a failure, so the framework
/// adds the exception type to the report trace afterwards, where the Allure categories look for it.
/// </summary>
internal static class FailureRegistry
{
    private static readonly ConcurrentDictionary<string, Exception> Last = new();

    public static void Record(Exception failure)
    {
        if (CurrentTestId() is { } id)
        {
            Last[id] = failure;
        }
    }

    public static Exception? Take(string testId) => Last.TryRemove(testId, out var failure) ? failure : null;

    private static string? CurrentTestId()
    {
        try
        {
            return TestContext.CurrentContext.Test.ID;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
