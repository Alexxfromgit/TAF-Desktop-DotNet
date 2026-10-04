using NUnit.Framework;

namespace Taf.Desktop.Core.Reporting;

/// <summary>Messages for the test output and, inside a test, the Allure report.</summary>
public static class Log
{
    public static void Info(string message)
    {
        TestContext.Out.WriteLine(message);
        Step.Report(message);
    }

    public static void Warn(string message)
    {
        TestContext.Progress.WriteLine("WARNING: " + message);
        TestContext.Out.WriteLine("WARNING: " + message);
        Step.Report("WARNING: " + message);
    }
}
