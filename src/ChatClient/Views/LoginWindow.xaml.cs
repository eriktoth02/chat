using System.Windows;

namespace ChatClient.Views;

public partial class LoginWindow : Window
{
    public LoginWindow() => InitializeComponent();

    private async void Login_Click(object sender, RoutedEventArgs e)
        => await DoAuthAsync(login: true);

    private async void Register_Click(object sender, RoutedEventArgs e)
        => await DoAuthAsync(login: false);

    private async Task DoAuthAsync(bool login)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Please enter a username and password.";
            return;
        }

        SetBusy(true);
        var (ok, error) = login
            ? await App.Api.LoginAsync(username, password)
            : await App.Api.RegisterAsync(username, password);
        SetBusy(false);

        if (!ok)
        {
            StatusText.Text = error ?? "Something went wrong.";
            return;
        }

        var main = new MainWindow();
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        RegisterButton.IsEnabled = !busy;
        UsernameBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        if (busy) StatusText.Text = "Please wait…";
    }
}
