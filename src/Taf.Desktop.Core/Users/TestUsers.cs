using Taf.Desktop.Core.Config;

namespace Taf.Desktop.Core.Users;

/// <summary>A test user; the password is masked in <see cref="ToString"/>.</summary>
public sealed record UserCredentials(string Alias, string Username, string Password)
{
    public override string ToString() => $"{Alias} ({Username}, password ****)";
}

/// <summary>
/// Test users by alias: <c>Users:{alias}:Username</c> from configuration, the password from the environment variable
/// <c>TAF__Users__{alias}__Password</c>.
/// </summary>
public static class TestUsers
{
    public static UserCredentials Get(string alias)
    {
        var config = TafConfig.Current;
        return new UserCredentials(alias, config.Get($"Users:{alias}:Username"), config.Secret($"Users:{alias}:Password"));
    }
}

/// <summary>The user a test runs as; used by preconditions and by the app lifecycle (a user change resets app data).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class TestUserAttribute(string alias) : Attribute
{
    public string Alias { get; } = alias;
}
