namespace ShopDesk.WinForms;

internal sealed class LoginForm : Form
{
    private readonly TextBox username = new TextBox { Width = 320 }.Named("UsernameBox");
    private readonly TextBox password = new TextBox { Width = 320, UseSystemPasswordChar = true }.Named("PasswordBox");
    private readonly CheckBox rememberMe = new CheckBox { Text = "Remember me", AutoSize = true }.Named("RememberMe");
    private readonly Label error = new Label { ForeColor = Color.Firebrick, AutoSize = true, MaximumSize = new Size(320, 0), Visible = false }.Named("ErrorText");
    private readonly Button signIn = new Button { Text = "Sign in", AutoSize = true }.Named("SignInButton");

    public LoginForm()
    {
        Name = "LoginWindow";
        Text = AppText.LoginTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AcceptButton = signIn;

        var title = new Label { Text = "Sign in to ShopDesk", AutoSize = true, Font = new Font(Font.FontFamily, 14) };
        Controls.Add(Ui.Column(title, Ui.Caption("User name"), username, Ui.Caption("Password"), password, rememberMe, error, signIn));

        var remembered = Services.Settings.RememberedUsername;
        if (!string.IsNullOrEmpty(remembered))
        {
            username.Text = remembered;
            rememberMe.Checked = true;
            Shown += (_, _) => password.Focus();
        }
        signIn.Click += OnSignIn;
    }

    private async void OnSignIn(object? sender, EventArgs e)
    {
        error.Visible = false;
        signIn.Enabled = false;
        var result = await Services.Api.LoginAsync(username.Text.Trim(), password.Text);
        signIn.Enabled = true;
        if (!result.Success)
        {
            error.Text = result.Error;
            error.Visible = true;
            return;
        }
        Services.Settings.RememberedUsername = rememberMe.Checked ? username.Text.Trim() : null;
        Services.DisplayName = result.DisplayName ?? username.Text;
        Services.Context.Show(new MainForm());
    }
}
