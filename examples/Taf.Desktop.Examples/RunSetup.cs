using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Examples.Backend;

namespace Taf.Desktop.Examples;

/// <summary>Once per run: the mock backend starts before the first test and stops after the last one.</summary>
[SetUpFixture]
public sealed class RunSetup : TafRunSetup
{
    private static readonly string[] DemoUsers = ["standard", "locked"];

    protected override void RunStarted()
    {
        UseGeneratedDemoPasswords();
        ShopBackend.Start();
    }

    /// <summary>
    /// The demo backend is a mock that accepts whatever password the test users have, so the examples generate one
    /// per run instead of storing any. Against a real backend, set TAF__Users__{alias}__Password instead.
    /// </summary>
    private static void UseGeneratedDemoPasswords()
    {
        foreach (var alias in DemoUsers)
        {
            var variable = TafConfig.SecretVariable($"Users:{alias}:Password");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            {
                Environment.SetEnvironmentVariable(variable, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)));
            }
        }
        TafConfig.Reload();
    }

    protected override void RunFinished() => ShopBackend.Stop();
}
