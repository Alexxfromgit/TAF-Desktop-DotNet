using System.Windows;

namespace ShopDesk.Wpf;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        var remembered = Services.Settings.RememberedUsername;
        if (!string.IsNullOrEmpty(remembered))
        {
            UsernameBox.Text = remembered;
            RememberMe.IsChecked = true;
            Loaded += (_, _) => PasswordInput.Focus();
        }
        else
        {
            Loaded += (_, _) => UsernameBox.Focus();
        }
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = false;
        var result = await Services.Api.LoginAsync(UsernameBox.Text.Trim(), PasswordInput.Password);
        SignInButton.IsEnabled = true;
        if (!result.Success)
        {
            ErrorText.Text = result.Error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Services.Settings.RememberedUsername = RememberMe.IsChecked == true ? UsernameBox.Text.Trim() : null;
        Services.DisplayName = result.DisplayName ?? UsernameBox.Text;
        new MainWindow().Show();
        Close();
    }
}
