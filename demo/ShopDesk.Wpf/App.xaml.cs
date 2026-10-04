using System.Windows;

namespace ShopDesk.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services.Init(LaunchOptions.Parse(e.Args));
        new LoginWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.Api.Dispose();
        base.OnExit(e);
    }
}
