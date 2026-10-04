using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Examples.Windows;

/// <summary>The menu bar: <c>Menu.Open("File", "Export cart...")</c>.</summary>
[Locate(AutomationId = "MainMenu")]
public sealed class MainMenu : Component
{
    public void Open(params string[] path) => Root.OpenMenu(path);
}

/// <summary>Mixin for windows with the main menu: any <see cref="IHasMainMenu"/> window gets a <c>Menu</c> property.</summary>
public interface IHasMainMenu : IUiContainer;

public static class HasMainMenuMixin
{
    extension(IHasMainMenu window)
    {
        public MainMenu Menu => window.Component<MainMenu>();
    }
}
