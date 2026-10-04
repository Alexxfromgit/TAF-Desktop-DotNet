using Taf.Desktop.Core.Ui;
using Taf.Desktop.Core.Users;

namespace Taf.Desktop.Examples.Windows;

[Locate(AutomationId = "LoginWindow")]
public sealed class LoginWindow : Window
{
    [WindowIdentifier]
    [Locate(AutomationId = "UsernameBox")]
    private UiElement username = null!;

    [Locate(AutomationId = "PasswordBox")]
    private UiElement password = null!;

    [Locate(AutomationId = "RememberMe")]
    private UiElement rememberMe = null!;

    [Locate(AutomationId = "SignInButton")]
    private UiElement signIn = null!;

    [Locate(AutomationId = "ErrorText")]
    private UiElement error = null!;

    public string Username => username.Text;

    public bool IsRememberMeChecked => rememberMe.IsChecked;

    public string Error => error.Text;

    public MainWindow SignInAs(UserCredentials user, bool remember = false)
    {
        EnterCredentials(user.Username, user.Password, remember);
        return signIn.ClickAndExpect<MainWindow>();
    }

    /// <summary>Signs in with credentials the server rejects and waits for the error message.</summary>
    public LoginWindow SignInExpectingError(string user, string pass)
    {
        EnterCredentials(user, pass, remember: false);
        signIn.Click();
        error.WaitVisible();
        return this;
    }

    private void EnterCredentials(string user, string pass, bool remember)
    {
        username.Type(user);
        password.Type(pass);
        rememberMe.SetChecked(remember);
    }
}
