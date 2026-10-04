using Allure.Net.Commons;
using NUnit.Framework;

namespace Taf.Desktop.Core.Reporting;

/// <summary>
/// Report steps. Prefer these over Allure's <c>[AllureStep]</c> and <c>AllureApi.Step</c>: with NUnit those wrap
/// failures in TargetInvocationException or report a test with a broken step as passed. Here an assertion marks the
/// step "failed", any other exception "broken", and the exception is rethrown unchanged.
/// </summary>
public static class Step
{
    public static void Run(string name, Action action) => Run<object?>(name, () =>
    {
        action();
        return null;
    });

    public static T Run<T>(string name, Func<T> action)
    {
        if (!InReport)
        {
            return action();
        }
        ExtendedApi.StartStep(name);
        try
        {
            var result = action();
            ExtendedApi.PassStep();
            return result;
        }
        catch (AssertionException e)
        {
            ExtendedApi.FailStep(e);
            throw;
        }
        catch (Exception e)
        {
            ExtendedApi.BreakStep(e);
            throw;
        }
    }

    /// <summary>A step without a body, e.g. a check that already happened.</summary>
    public static void Report(string name, bool passed = true)
    {
        if (!InReport)
        {
            return;
        }
        ExtendedApi.StartStep(name);
        if (passed)
        {
            ExtendedApi.PassStep();
        }
        else
        {
            ExtendedApi.FailStep();
        }
    }

    /// <summary>True while an Allure test or fixture is running (steps outside of one are ignored).</summary>
    public static bool InReport
    {
        get
        {
            try
            {
                var context = AllureLifecycle.Instance.Context;
                return context.HasTest || context.HasFixture;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
