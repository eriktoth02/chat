using System.Windows;
using ChatClient.Services;
using ChatClient.Views;

namespace ChatClient;

public partial class App : Application
{
    /// <summary>Base URL of the ChatApi. Must match the API's configured Urls/applicationUrl.</summary>
    public const string ApiBaseUrl = "http://localhost:5099";

    /// <summary>Single shared API client (holds the session token after login).</summary>
    public static ChatApiClient Api { get; } = new ChatApiClient(ApiBaseUrl);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var login = new LoginWindow();
        MainWindow = login;
        login.Show();
    }
}
