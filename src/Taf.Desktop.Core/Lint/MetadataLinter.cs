using System.Reflection;
using Allure.Net.Commons.Attributes;
using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Lint;

public sealed record LintViolation(string Where, string Rule)
{
    public override string ToString() => $"{Where}: {Rule}";
}

/// <summary>
/// Static checks of a test assembly, run as a normal test (no app needed):
/// <list type="bullet">
/// <item>every test has an owner ([AllureOwner]) and a feature or story ([AllureFeature]/[AllureStory]) on the method or class;</item>
/// <item>every window has a class-level [Locate] and at least one [WindowIdentifier];</item>
/// <item>[Quarantined] dates are valid and at most <c>Lint:QuarantineMaxDays</c> ahead; [KnownIssue] ids are not empty;</item>
/// <item>no secret values in <c>taf*.json</c>.</item>
/// </list>
/// </summary>
public sealed class MetadataLinter
{
    private readonly IReadOnlyList<Type> types;
    private readonly TafConfig config;

    private MetadataLinter(IReadOnlyList<Type> types, TafConfig config)
    {
        this.types = types;
        this.config = config;
    }

    public static MetadataLinter ForAssembly(Assembly assembly, TafConfig? config = null) =>
        new(assembly.GetTypes(), config ?? TafConfig.Current);

    /// <summary>Checks only the given types (and the configuration files).</summary>
    public static MetadataLinter ForTypes(IEnumerable<Type> types, TafConfig? config = null) =>
        new(types.ToList(), config ?? TafConfig.Current);

    public IReadOnlyList<LintViolation> Check()
    {
        var violations = new List<LintViolation>();
        foreach (var type in types.Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public).Where(IsTest))
            {
                CheckTest(type, method, violations);
            }
            if (typeof(Window).IsAssignableFrom(type) && config.Bool("Lint:RequireWindowIdentifier", true))
            {
                CheckWindow(type, violations);
            }
        }
        if (config.Bool("Lint:Secrets", true))
        {
            violations.AddRange(SecretsGuard.Scan(config.BaseDirectory).Select(v => new LintViolation(v, "secret value in a configuration file; read it with TafConfig.Secret")));
        }
        return violations;
    }

    /// <summary>Fails with all violations, one per line.</summary>
    public void AssertClean()
    {
        var violations = Check();
        if (violations.Count > 0)
        {
            Assert.Fail($"{violations.Count} metadata violation(s):{Environment.NewLine}" + string.Join(Environment.NewLine, violations));
        }
    }

    private void CheckTest(Type type, MethodInfo method, List<LintViolation> violations)
    {
        var where = $"{type.Name}.{method.Name}";
        if (config.Bool("Lint:RequireOwner", true) && !Has<AllureOwnerAttribute>(type, method))
        {
            violations.Add(new LintViolation(where, "needs an owner: [AllureOwner(\"team\")] on the method or class"));
        }
        if (config.Bool("Lint:RequireFeature", true) && !Has<AllureFeatureAttribute>(type, method) && !Has<AllureStoryAttribute>(type, method))
        {
            violations.Add(new LintViolation(where, "needs a feature: [AllureFeature(\"...\")] or [AllureStory(\"...\")] on the method or class"));
        }
        var quarantine = method.GetCustomAttribute<QuarantinedAttribute>() ?? type.GetCustomAttribute<QuarantinedAttribute>();
        if (quarantine != null)
        {
            var maxDays = config.Int("Lint:QuarantineMaxDays", 90);
            if (quarantine.Until is not { } until)
            {
                violations.Add(new LintViolation(where, $"[Quarantined] date '{quarantine.UntilText}' is not yyyy-MM-dd"));
            }
            else if (until.DayNumber - QuarantinedAttribute.Today().DayNumber > maxDays)
            {
                violations.Add(new LintViolation(where, $"[Quarantined] until {quarantine.UntilText} is more than {maxDays} days ahead"));
            }
            if (string.IsNullOrWhiteSpace(quarantine.Reason))
            {
                violations.Add(new LintViolation(where, "[Quarantined] needs a reason"));
            }
        }
        if (method.GetCustomAttribute<KnownIssueAttribute>() is { } issue && string.IsNullOrWhiteSpace(issue.Id))
        {
            violations.Add(new LintViolation(where, "[KnownIssue] needs an issue id"));
        }
    }

    private static void CheckWindow(Type type, List<LintViolation> violations)
    {
        if (type.GetCustomAttribute<LocateAttribute>() == null)
        {
            violations.Add(new LintViolation(type.Name, "a window needs a class-level [Locate]"));
        }
        var hasIdentifier = false;
        for (var t = type; t != null && t != typeof(Window); t = t.BaseType)
        {
            hasIdentifier |= t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Any(f => f.GetCustomAttribute<WindowIdentifierAttribute>() != null);
        }
        if (!hasIdentifier)
        {
            violations.Add(new LintViolation(type.Name, "a window needs at least one [WindowIdentifier] element (proves it is open)"));
        }
    }

    private static bool IsTest(MethodInfo method) =>
        method.GetCustomAttributes().Any(a => a is TestAttribute or TestCaseAttribute or TestCaseSourceAttribute or TheoryAttribute);

    private static bool Has<T>(Type type, MethodInfo method) where T : Attribute =>
        method.GetCustomAttribute<T>(inherit: true) != null || type.GetCustomAttribute<T>(inherit: true) != null;
}
