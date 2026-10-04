using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Evidence;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Preconditions;
using Taf.Desktop.Core.Reporting;
using Taf.Desktop.Core.Ui;
using Taf.Desktop.Core.Users;

namespace Taf.Desktop.Core.Testing;

/// <summary>
/// Base class of UI tests. Before each test it prepares the app according to the lifecycle policy (start, restart,
/// reset data or reuse) and establishes the [Precondition]s; after a failure it attaches a screenshot and the UI
/// tree, and optionally a screen recording.
/// </summary>
public abstract class DesktopTest : TafTest
{
    /// <summary>The running app.</summary>
    protected static AppSession App => AppSession.Current ?? throw new FrameworkException("The app is not running");

    /// <summary>Credentials of the test's [TestUser].</summary>
    protected static UserCredentials User => TestScope.Current.User;

    /// <summary>The window of type <typeparamref name="T"/> once it is ready.</summary>
    protected static T On<T>() where T : Window => WindowFactory.On<T>();

    protected sealed override void BeforeTest(TestScope scope)
    {
        try
        {
            BeforeAppStart(scope);
            AppLifecycle.Prepare(scope);
            ScreenRecorder.Start(scope.FullName);
            foreach (var precondition in scope.Attributes<PreconditionAttribute>().OrderBy(p => p.Order))
            {
                Step.Run($"Precondition: {precondition.Description}", () => precondition.Establish(new PreconditionContext(scope)));
            }
        }
        catch (Exception)
        {
            // NUnit skips [TearDown] when [SetUp] fails: collect the evidence here.
            FailureEvidence.Capture("of failed set-up");
            ScreenRecorder.Stop(failed: true);
            AppLifecycle.Finished(failed: true);
            throw;
        }
    }

    protected sealed override void AfterTest(TestScope scope, bool failed)
    {
        try
        {
            if (failed)
            {
                FailureEvidence.Capture();
            }
            AfterTestBody(scope, failed);
        }
        finally
        {
            ScreenRecorder.Stop(failed);
            AppLifecycle.Finished(failed);
        }
    }

    /// <summary>Runs before the app is prepared: configure stubs of a mock backend, test data, ...</summary>
    protected virtual void BeforeAppStart(TestScope scope)
    {
    }

    /// <summary>Runs after each test while the app is still open (evidence of failures is already attached).</summary>
    protected virtual void AfterTestBody(TestScope scope, bool failed)
    {
    }
}
