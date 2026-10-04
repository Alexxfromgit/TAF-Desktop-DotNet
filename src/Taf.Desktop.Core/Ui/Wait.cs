using System.Diagnostics;
using System.Runtime.InteropServices;
using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.Ui;

/// <summary>Explicit waits (no implicit waits anywhere): poll a condition until it holds or the timeout expires.</summary>
public static class Wait
{
    public static TimeSpan Timeout => TafConfig.Current.Duration("Wait:Timeout");

    public static TimeSpan WindowTimeout => TafConfig.Current.Duration("Wait:WindowTimeout");

    public static TimeSpan Poll => TafConfig.Current.Duration("Wait:Poll");

    /// <summary>True as soon as <paramref name="condition"/> holds; elements vanishing while polled count as "not yet".</summary>
    public static bool Until(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? Timeout;
        var poll = Poll;
        var watch = Stopwatch.StartNew();
        while (true)
        {
            if (Check(condition))
            {
                return true;
            }
            if (watch.Elapsed >= limit)
            {
                return false;
            }
            Thread.Sleep(poll);
        }
    }

    /// <summary>The first non-null result of <paramref name="probe"/>, or null after the timeout.</summary>
    public static T? For<T>(Func<T?> probe, TimeSpan? timeout = null) where T : class
    {
        T? result = null;
        Until(() => (result = probe()) != null, timeout);
        return result;
    }

    internal static bool Check(Func<bool> condition)
    {
        try
        {
            return condition();
        }
        catch (Exception e) when (IsTransient(e))
        {
            return false;
        }
    }

    /// <summary>UI Automation errors caused by an element that changed or disappeared while it was read.</summary>
    internal static bool IsTransient(Exception e) =>
        e is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException or FlaUI.Core.Exceptions.PropertyNotSupportedException
            or FlaUI.Core.Exceptions.PatternNotSupportedException;
}
