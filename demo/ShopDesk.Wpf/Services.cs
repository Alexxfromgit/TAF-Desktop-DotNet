namespace ShopDesk.Wpf;

/// <summary>App-wide state: backend client, settings and the cart of the signed-in user.</summary>
internal static class Services
{
    public static LaunchOptions Options { get; private set; } = null!;

    public static ShopApi Api { get; private set; } = null!;

    public static SettingsStore Settings { get; private set; } = null!;

    public static Cart Cart { get; } = new();

    public static string DisplayName { get; set; } = "";

    public static void Init(LaunchOptions options)
    {
        Options = options;
        Api = new ShopApi(options.ApiUrl);
        Settings = new SettingsStore(options.DataDirectory);
    }
}
