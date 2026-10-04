using System.Collections.Concurrent;
using System.Reflection;
using NUnit.Framework;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Users;

namespace Taf.Desktop.Core.Testing;

/// <summary>State of the running test: its method and attributes, user, soft failures and free-form data.</summary>
public sealed class TestScope
{
    private static readonly ConcurrentDictionary<string, TestScope> Scopes = new();

    private TestScope(string testId, string fullName, MethodInfo? method, Type? fixture)
    {
        TestId = testId;
        FullName = fullName;
        Method = method;
        Fixture = fixture;
    }

    /// <summary>The scope of the running test; fails outside a test of a <see cref="TafTest"/> fixture.</summary>
    public static TestScope Current => TryCurrent ?? throw new FrameworkException("No test is running (TestScope is created by TafTest)");

    public static TestScope? TryCurrent
    {
        get
        {
            try
            {
                return Scopes.GetValueOrDefault(TestContext.CurrentContext.Test.ID);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public string TestId { get; }

    public string FullName { get; }

    public MethodInfo? Method { get; }

    public Type? Fixture { get; }

    /// <summary>Alias of the [TestUser] of the test or its class.</summary>
    public string? UserAlias => Attribute<TestUserAttribute>()?.Alias;

    /// <summary>Credentials of the [TestUser]; fails if the test has none.</summary>
    public UserCredentials User => UserAlias is { } alias
        ? TestUsers.Get(alias)
        : throw new TestDataException($"{FullName} needs a [TestUser(\"alias\")] attribute");

    public int SoftFailures { get; internal set; }

    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>();

    /// <summary>The attribute on the test method, else on its class (base classes included).</summary>
    public T? Attribute<T>() where T : Attribute =>
        Method?.GetCustomAttribute<T>(inherit: true) ?? Fixture?.GetCustomAttribute<T>(inherit: true);

    /// <summary>All attributes of a type on the class and the method, class first.</summary>
    public IReadOnlyList<T> Attributes<T>() where T : Attribute =>
        (Fixture?.GetCustomAttributes<T>(inherit: true) ?? []).Concat(Method?.GetCustomAttributes<T>(inherit: true) ?? []).ToList();

    internal static TestScope Start()
    {
        var test = global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest;
        var scope = new TestScope(test.Id, test.FullName, test.Method?.MethodInfo, test.TypeInfo?.Type);
        Scopes[test.Id] = scope;
        return scope;
    }

    internal static void End(string testId) => Scopes.TryRemove(testId, out _);
}
