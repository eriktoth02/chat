using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class MainWindow : Window
{
    private readonly ChatViewModel _vm;
    private bool _prepending;
    private bool _atBottom = true;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new ChatViewModel(App.Api);
        _vm.MessagesAppended += OnMessagesAppended;
        _vm.HistoryPrepended += () => _prepending = true;
        _vm.LoggedOut += ReturnToLogin;
        DataContext = _vm;

        Loaded += async (_, _) => await _vm.InitializeAsync();
    }

    private void OnMessagesAppended(int incomingCount, bool force)
    {
        if (force || _atBottom)
        {
            MessageScroll.ScrollToBottom();
            _vm.UnreadCount = 0;
        }
        else if (incomingCount > 0)
        {
            _vm.UnreadCount += incomingCount;
        }
    }

    private async void MessageScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // After older history is prepended, keep the viewport where the user was
        // (offset grows by the height of the newly added content).
        if (_prepending && e.ExtentHeightChange > 0)
        {
            MessageScroll.ScrollToVerticalOffset(e.ExtentHeightChange);
            _prepending = false;
            return;
        }

        _atBottom = MessageScroll.VerticalOffset >= MessageScroll.ScrollableHeight - 2;
        if (_atBottom && _vm.UnreadCount > 0)
            _vm.UnreadCount = 0;

        // User scrolled to the very top (not a content change) -> load older history.
        if (e.ExtentHeightChange == 0 && MessageScroll.VerticalOffset <= 0.5)
            await _vm.LoadOlderAsync();
    }

    private void UnreadPill_Click(object sender, RoutedEventArgs e)
    {
        MessageScroll.ScrollToBottom();
        _vm.UnreadCount = 0;
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        var win = new ProfileWindow { Owner = this };
        win.ShowDialog();
        _vm.RefreshIdentity();
    }

    private async void Attach_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose a file to send" };
        if (dlg.ShowDialog() == true)
            await _vm.SendAttachmentAsync(dlg.FileName);
    }

    private void Attachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string url && !string.IsNullOrEmpty(url))
            OpenInBrowser(url);
    }

    private void DmList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DmList.SelectedItem is DmUserItem u)
        {
            var win = new DirectChatWindow(u.UserId, u.Name);
            win.Show();
        }
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }

    private void ReturnToLogin()
    {
        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.SendCommand.CanExecute(null))
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.CancelEdit();
            e.Handled = true;
        }
    }
}
