using System.Windows;

namespace ChatClient.Views;

public partial class ProfileWindow : Window
{
    public ProfileWindow()
    {
        InitializeComponent();
        DisplayNameBox.Text = App.Api.DisplayName ?? "";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameBox.Text.Trim();
        var newPassword = NewPasswordBox.Password;

        SetBusy(true);
        var (ok, error) = await App.Api.UpdateProfileAsync(
            displayName,
            string.IsNullOrEmpty(newPassword) ? null : newPassword);
        SetBusy(false);

        if (!ok)
        {
            StatusText.Text = error ?? "Could not save profile.";
            return;
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy)
    {
        SaveButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        if (busy) StatusText.Text = "Saving…";
    }
}
