using System.Security.Cryptography;
using System.Text;
using Allure.Net.Commons;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Testing;

/// <summary>
/// Base class of framework tests (Allure reporting included). Per test it:
/// <list type="bullet">
/// <item>creates the <see cref="TestScope"/>;</item>
/// <item>adds report parameters and labels from <c>Report:Parameters</c> / <c>Report:Labels</c> (e.g. which app flavour ran);</item>
/// <item>links [KnownIssue] and maps priority categories to Allure severity;</item>
/// <item>after the test: classifies the failure for the report (failed vs broken, type in the trace).</item>
/// </list>
/// Derive UI tests from <c>DesktopTest</c>, which adds the app lifecycle and failure evidence. A [Quarantined]
/// test is skipped here, before anything starts, so the report lists it as skipped with its reason.
/// </summary>
[AllureNUnit]
public abstract class TafTest
{
    [SetUp]
    public void TafSetUp()
    {
        var scope = TestScope.Start();
        if (scope.Attribute<QuarantinedAttribute>() is { IsActive: true } quarantine)
        {
            TestScope.End(scope.TestId);
            Assert.Ignore($"Quarantined until {quarantine.UntilText}: {quarantine.Reason}");
        }
        try
        {
            ApplyReportMetadata(scope);
            BeforeTest(scope);
        }
        catch (Exception)
        {
            TestScope.End(scope.TestId);
            throw;
        }
    }

    [TearDown]
    public void TafTearDown()
    {
        var scope = TestScope.TryCurrent;
        var failed = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed;
        try
        {
            if (scope != null)
            {
                AfterTest(scope, failed);
            }
        }
        finally
        {
            if (scope?.Attribute<KnownIssueAttribute>() is { } issue && !failed)
            {
                Log.Warn($"Test passes although it is marked [KnownIssue(\"{issue.Id}\")] - is the bug fixed? Remove the attribute.");
            }
            FailureNormalizer.Normalize();
            if (scope != null)
            {
                TestScope.End(scope.TestId);
            }
        }
    }

    /// <summary>Runs after the framework set-up of each test, before the [SetUp] methods of derived classes.</summary>
    protected virtual void BeforeTest(TestScope scope)
    {
    }

    /// <summary>Runs after each test (also failed ones), before the framework classifies the result.</summary>
    protected virtual void AfterTest(TestScope scope, bool failed)
    {
    }

    private static void ApplyReportMetadata(TestScope scope)
    {
        if (!Step.InReport)
        {
            return;
        }
        var config = TafConfig.Current;
        var parameters = config.Section("Report:Parameters");
        var labels = config.Section("Report:Labels");
        var issue = scope.Attribute<KnownIssueAttribute>();
        var severity = scope.Attribute<Allure.Net.Commons.Attributes.AllureSeverityAttribute>() == null ? TestCategories.SeverityOf(TestContext.CurrentContext.Test.Properties["Category"]) : null;
        AllureLifecycle.Instance.UpdateTestCase(test =>
        {
            foreach (var (name, value) in parameters)
            {
                test.parameters.Add(new Parameter { name = name, value = value });
            }
            foreach (var (name, value) in labels)
            {
                test.labels.RemoveAll(l => l.name == name);
                test.labels.Add(new Label { name = name, value = value });
            }
            if (severity != null)
            {
                test.labels.RemoveAll(l => l.name == "severity");
                test.labels.Add(new Label { name = "severity", value = severity });
            }
            if (issue != null)
            {
                test.links.Add(issue.Url is { } url ? new Link { name = issue.Id, type = "issue", url = url } : Link.Issue(issue.Id));
                test.name = $"[KNOWN {issue.Id}] {test.name}";
            }
            if (parameters.Count > 0)
            {
                // Runs of the same test with other parameters (another app flavour) are separate tests, not retries.
                var key = test.fullName + "|" + string.Join("|", parameters.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                test.historyId = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
            }
        });
    }
}

/// <summary>Category names used by the examples; priority categories become the Allure severity.</summary>
public static class TestCategories
{
    public const string Smoke = "smoke";
    public const string Regression = "regression";

    /// <summary>Tests that need no app (linter, configuration checks): run in every CI build.</summary>
    public const string Static = "static";

    public const string Blocker = "blocker";
    public const string Critical = "critical";
    public const string Minor = "minor";

    internal static string? SeverityOf(IEnumerable<object> categoryValues)
    {
        var categories = categoryValues.OfType<string>().ToList();
        return new[] { Blocker, Critical, Minor }.FirstOrDefault(categories.Contains);
    }
}
