using System.Windows;
using System.Windows.Input;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class DirectChatWindow : Window
{
    private readonly DirectChatViewModel _vm;

    public DirectChatWindow(int otherId, string otherName)
    {
        InitializeComponent();
        _vm = new DirectChatViewModel(App.Api, otherId, otherName);
        _vm.MessagesChanged += () => Scroll.ScrollToBottom();
        DataContext = _vm;

        Loaded += async (_, _) => await _vm.StartAsync();
        Closed += (_, _) => _vm.Stop();
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.SendCommand.CanExecute(null))
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
