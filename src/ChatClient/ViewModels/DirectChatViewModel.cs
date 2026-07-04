using System.Collections.ObjectModel;
using System.Windows.Threading;
using ChatClient.Services;

namespace ChatClient.ViewModels;

/// <summary>Drives a single 1:1 direct-message conversation window.</summary>
public class DirectChatViewModel : ViewModelBase
{
    private readonly ChatApiClient _api;
    private readonly int _otherId;
    private readonly DispatcherTimer _timer;
    private bool _polling;

    public string OtherName { get; }
    public ObservableCollection<DmItem> Messages { get; } = new();

    public event Action? MessagesChanged;

    public RelayCommand SendCommand { get; }

    public DirectChatViewModel(ChatApiClient api, int otherId, string otherName)
    {
        _api = api;
        _otherId = otherId;
        OtherName = otherName;

        SendCommand = new RelayCommand(
            async _ => await SendAsync(),
            _ => !string.IsNullOrWhiteSpace(MessageText));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    private string _messageText = "";
    public string MessageText
    {
        get => _messageText;
        set => Set(ref _messageText, value);
    }

    public async Task StartAsync()
    {
        await PollAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            // Fetch the whole conversation each tick (small) so delivered/read
            // status on already-shown messages updates too.
            var all = await _api.GetDmMessagesAsync(_otherId, 0);

            bool added = false;
            bool incomingUnread = false;
            foreach (var d in all)
            {
                var item = Find(d.Id);
                if (item == null)
                {
                    Messages.Add(new DmItem
                    {
                        Id = d.Id,
                        Content = d.Content,
                        SentUtc = d.SentUtc,
                        Mine = d.Mine,
                        DeliveredUtc = d.DeliveredUtc,
                        ReadUtc = d.ReadUtc
                    });
                    added = true;
                }
                else
                {
                    item.DeliveredUtc = d.DeliveredUtc;
                    item.ReadUtc = d.ReadUtc;
                }
                if (!d.Mine && d.ReadUtc == null) incomingUnread = true;
            }

            if (incomingUnread) await _api.MarkDmReadAsync(_otherId);
            if (added) MessagesChanged?.Invoke();
        }
        catch { /* best effort */ }
        finally { _polling = false; }
    }

    private DmItem? Find(int id)
    {
        foreach (var m in Messages) if (m.Id == id) return m;
        return null;
    }

    private async Task SendAsync()
    {
        var text = MessageText.Trim();
        if (text.Length == 0) return;
        MessageText = "";
        var sent = await _api.SendDmAsync(_otherId, text);
        if (sent != null) await PollAsync();
    }
}
