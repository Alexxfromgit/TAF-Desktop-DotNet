using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;

namespace Taf.Desktop.Core.Tests;

public class TafConfigTests
{
    [SetUp]
    public void Reset() => RuntimeOverrides.Clear();

    [TearDown]
    public void Restore() => TestConfig.UseDefaults();

    [Test]
    public void DefaultsComeFromTheFramework() =>
        TestConfig.Load().Get("App:Lifecycle").Should().Be("RestartApp");

    [Test]
    public void TafJsonOverridesDefaults() =>
        TestConfig.Load().Duration("Wait:Timeout").Should().Be(TimeSpan.FromMilliseconds(300));

    [Test]
    public void EnvFileOverridesTafJson()
    {
        var config = TestConfig.Load(env: "unit");

        config.Env.Should().Be("unit");
        config.Get("Layer:Env").Should().Be("from taf.unit.json");
        config.Get("Layer:Base").Should().Be("from taf.json");
    }

    [Test]
    public void EnvIsReadFromTafEnvVariableOrTestParameter()
    {
        TestConfig.Load(environment: new Dictionary<string, string?> { ["TAF_ENV"] = "unit" }).Env.Should().Be("unit");
        TestConfig.Load(parameters: new Dictionary<string, string?> { ["env"] = "unit" }).Env.Should().Be("unit");
    }

    [Test]
    public void EnvironmentVariablesOverrideFiles() =>
        TestConfig.Load(environment: new Dictionary<string, string?> { ["TAF__Layer__Base"] = "from env" })
            .Get("Layer:Base").Should().Be("from env");

    [Test]
    public void TestParametersOverrideEnvironmentVariables() =>
        TestConfig.Load(new Dictionary<string, string?> { ["TAF__Layer__Base"] = "from env" },
                new Dictionary<string, string?> { ["Layer:Base"] = "from parameter" })
            .Get("Layer:Base").Should().Be("from parameter");

    [Test]
    public void RuntimeOverridesWin()
    {
        var config = TestConfig.Load(parameters: new Dictionary<string, string?> { ["Layer:Base"] = "from parameter" });
        RuntimeOverrides.Set("Layer:Base", "overridden");

        config.Get("Layer:Base").Should().Be("overridden");
    }

    [Test]
    public void ReferencesAreResolved()
    {
        var config = TestConfig.Load(parameters: new Dictionary<string, string?> { ["App:Arguments"] = "--api ${Mock:Url} --env ${Layer:Base}" });
        RuntimeOverrides.Set("Mock:Url", "http://localhost:1234");

        config.Get("App:Arguments").Should().Be("--api http://localhost:1234 --env from taf.json");
    }

    [Test]
    public void MissingReferenceNamesBothKeys()
    {
        var config = TestConfig.Load(parameters: new Dictionary<string, string?> { ["App:Arguments"] = "--api ${Mock:Url}" });

        config.Invoking(c => c.Get("App:Arguments")).Should().Throw<FrameworkException>()
            .WithMessage("'App:Arguments' references ${Mock:Url}, which is not set");
    }

    [Test]
    public void MissingKeyTellsWhereToSetIt() =>
        TestConfig.Load().Invoking(c => c.Get("Nope:Key")).Should().Throw<FrameworkException>()
            .WithMessage("*Missing configuration 'Nope:Key'*TAF__Nope__Key*");

    [Test]
    public void SecretsComeOnlyFromEnvironmentVariables()
    {
        var config = TestConfig.Load(new Dictionary<string, string?> { ["TAF__Users__standard__Password"] = "s3cret" },
            new Dictionary<string, string?> { ["Users:standard:Password"] = "from parameter" });

        config.Secret("Users:standard:Password").Should().Be("s3cret");
    }

    [Test]
    public void MissingSecretNamesTheVariable() =>
        TestConfig.Load().Invoking(c => c.Secret("Users:admin:Password")).Should().Throw<EnvironmentException>()
            .WithMessage("*TAF__Users__admin__Password*");

    [TestCase("500ms", 500)]
    [TestCase("10s", 10_000)]
    [TestCase("1.5s", 1_500)]
    [TestCase("2m", 120_000)]
    [TestCase("00:00:03", 3_000)]
    public void DurationsHaveShortAndLongForms(string text, int millis) =>
        TafConfig.ParseDuration(text).Should().Be(TimeSpan.FromMilliseconds(millis));

    [Test]
    public void InvalidDurationIsExplained() =>
        FluentActions.Invoking(() => TafConfig.ParseDuration("soon")).Should().Throw<FrameworkException>().WithMessage("*examples: 500ms*");

    [Test]
    public void EnumsAreParsedCaseInsensitively() =>
        TestConfig.Load(parameters: new Dictionary<string, string?> { ["App:Lifecycle"] = "reuse" })
            .Enum("App:Lifecycle", App.StartMode.RestartApp).Should().Be(App.StartMode.Reuse);

    [Test]
    public void SectionListsChildKeys() =>
        TestConfig.Load(parameters: new Dictionary<string, string?> { ["Report:Parameters:App"] = "WPF", ["Report:Parameters:Theme"] = "Dark" })
            .Section("Report:Parameters").Should().BeEquivalentTo(new Dictionary<string, string> { ["App"] = "WPF", ["Theme"] = "Dark" });

    [Test]
    public void RelativePathsResolveAgainstTheRepositoryRoot()
    {
        var config = TestConfig.Load();

        File.Exists(Path.Combine(config.RootDirectory, "global.json")).Should().BeTrue();
        config.ResolvePath("demo/app.exe").Should().Be(Path.Combine(config.RootDirectory, "demo", "app.exe"));
    }
}

public class SecretsGuardTests
{
    [Test]
    public void FindsSecretValuesInNestedKeys() =>
        SecretsGuard.ScanJson("""{ "Users": { "admin": { "Username": "a", "Password": "p" } }, "Api": { "Token": "" } }""")
            .Should().Equal("Users:admin:Password");

    [TestCase("Password", true)]
    [TestCase("ClientSecret", true)]
    [TestCase("api-key", true)]
    [TestCase("AuthToken", true)]
    [TestCase("Username", false)]
    public void RecognisesSecretKeys(string key, bool secret) => SecretsGuard.LooksLikeSecret(key).Should().Be(secret);

    [Test]
    public void LoadingFailsWhenAFileContainsASecret()
    {
        var dir = Directory.CreateTempSubdirectory("taf-guard").FullName;
        File.WriteAllText(Path.Combine(dir, "taf.json"), """{ "Users": { "admin": { "Password": "oops" } } }""");

        FluentActions.Invoking(() => TafConfig.Load(dir, null, new Dictionary<string, string?>(), new Dictionary<string, string?>()))
            .Should().Throw<FrameworkException>().WithMessage("*taf.json: Users:admin:Password*");
    }
}
