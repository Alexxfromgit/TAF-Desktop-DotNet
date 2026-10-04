namespace ShopDesk.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Services.Init(LaunchOptions.Parse(args));
        using var context = new ShopDeskContext();
        Application.Run(context);
        Services.Api.Dispose();
    }
}

/// <summary>Switches between the sign-in and main forms; the app exits when the last one closes.</summary>
internal sealed class ShopDeskContext : ApplicationContext
{
    public ShopDeskContext()
    {
        Current = this;
        Show(new LoginForm());
    }

    public static ShopDeskContext Current { get; private set; } = null!;

    public void Show(Form next)
    {
        var previous = MainForm;
        MainForm = next;
        next.FormClosed += (_, _) =>
        {
            if (MainForm == next)
            {
                ExitThread();
            }
        };
        next.Show();
        previous?.Close();
    }
}

/// <summary>App-wide state: backend client, settings and the cart of the signed-in user.</summary>
internal static class Services
{
    public static LaunchOptions Options { get; private set; } = null!;

    public static ShopApi Api { get; private set; } = null!;

    public static SettingsStore Settings { get; private set; } = null!;

    public static Cart Cart { get; } = new();

    public static string DisplayName { get; set; } = "";

    public static ShopDeskContext Context => ShopDeskContext.Current;

    public static void Init(LaunchOptions options)
    {
        Options = options;
        Api = new ShopApi(options.ApiUrl);
        Settings = new SettingsStore(options.DataDirectory);
    }
}
