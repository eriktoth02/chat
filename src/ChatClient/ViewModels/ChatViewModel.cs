using System.Collections.ObjectModel;
using System.Media;
using System.Windows.Threading;
using ChatClient.Models;
using ChatClient.Services;

namespace ChatClient.ViewModels;

/// <summary>
/// Drives the main chat window. Real-time feel is achieved with three polling
/// timers (new messages every 1s, presence every 2s, heartbeat every 3s) plus
/// throttled "typing" notifications. Polls are incremental (afterId) to keep
/// API load low.
/// </summary>
public class ChatViewModel : ViewModelBase
{
    private readonly ChatApiClient _api;

    private readonly DispatcherTimer _messageTimer;
    private readonly DispatcherTimer _presenceTimer;
    private readonly DispatcherTimer _heartbeatTimer;
    private readonly DispatcherTimer _reconcileTimer;

    private int _lastMessageId;
    private int _firstMessageId;
    private bool _pollingMessages;
    private bool _pollingPresence;
    private bool _loadingOlder;
    private DateTime _lastTypingSent = DateTime.MinValue;
    private MessageItem? _editing;

    public ObservableCollection<RoomDto> Rooms { get; } = new();
    // Holds MessageItem and DateSeparator objects (rendered by type via DataTemplates).
    public ObservableCollection<object> Messages { get; } = new();
    public ObservableCollection<string> OnlineUsers { get; } = new();
    public ObservableCollection<DmUserItem> DmUsers { get; } = new();

    public string CurrentUsername =>
        string.IsNullOrWhiteSpace(_api.DisplayName) ? _api.Username : _api.DisplayName!;

    /// <summary>Call after the profile changes so the sidebar name refreshes.</summary>
    public void RefreshIdentity() => OnPropertyChanged(nameof(CurrentUsername));

    /// <summary>Raised when new messages are appended: (incomingCount, forceScroll).</summary>
    public event Action<int, bool>? MessagesAppended;

    /// <summary>Raised after older history is prepended so the view can keep its scroll position.</summary>
    public event Action? HistoryPrepended;

    /// <summary>Raised after logout so the shell can return to the login window.</summary>
    public event Action? LoggedOut;

    public RelayCommand SendCommand { get; }
    public RelayCommand CreateRoomCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand InsertEmojiCommand { get; }
    public RelayCommand EditMessageCommand { get; }
    public RelayCommand CancelEditCommand { get; }

    public ChatViewModel(ChatApiClient api)
    {
        _api = api;

        SendCommand = new RelayCommand(
            async _ => await SendAsync(),
            _ => SelectedRoom != null && !string.IsNullOrWhiteSpace(MessageText));

        CreateRoomCommand = new RelayCommand(
            async _ => await CreateRoomAsync(),
            _ => !string.IsNullOrWhiteSpace(NewRoomName));

        LogoutCommand = new RelayCommand(async _ => await LogoutAsync());

        InsertEmojiCommand = new RelayCommand(e =>
        {
            if (e is string emoji) MessageText += emoji;
        });

        EditMessageCommand = new RelayCommand(
            e => BeginEdit(e as MessageItem),
            e => e is MessageItem m && m.IsMine && !m.HasAttachment);

        CancelEditCommand = new RelayCommand(_ => CancelEdit());

        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _messageTimer.Tick += async (_, _) => await PollMessagesAsync();

        _presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _presenceTimer.Tick += async (_, _) =>
        {
            await PollPresenceAsync();
            await LoadRoomsAsync();       // pick up rooms created by other users
            await LoadDmOverviewAsync();  // refresh direct-message unread badges
        };

        _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _heartbeatTimer.Tick += async (_, _) => await HeartbeatAsync();

        // Re-fetch the latest page periodically so edits made by others show up.
        _reconcileTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _reconcileTimer.Tick += async (_, _) => await ReconcileRecentAsync();
    }

    // ---------- Editing state ----------

    public bool IsEditing => _editing != null;
    public string SendButtonText => IsEditing ? "Save" : "Send";

    // ---------- Bindable properties ----------

    private RoomDto? _selectedRoom;
    public RoomDto? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (Set(ref _selectedRoom, value))
                _ = SwitchRoomAsync();
        }
    }

    private string _messageText = "";
    public string MessageText
    {
        get => _messageText;
        set
        {
            if (Set(ref _messageText, value))
                _ = NotifyTypingAsync();
        }
    }

    private string _newRoomName = "";
    public string NewRoomName
    {
        get => _newRoomName;
        set => Set(ref _newRoomName, value);
    }

    private string _typingIndicator = "";
    public string TypingIndicator
    {
        get => _typingIndicator;
        set => Set(ref _typingIndicator, value);
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    private int _unreadCount;
    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (Set(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(UnreadText));
            }
        }
    }
    public bool HasUnread => _unreadCount > 0;
    public string UnreadText => $"↓ {_unreadCount} new";

    // ---------- Lifecycle ----------

    public async Task InitializeAsync()
    {
        await LoadRoomsAsync();
        await LoadDmOverviewAsync();
        if (Rooms.Count > 0)
            SelectedRoom = Rooms[0];

        _messageTimer.Start();
        _presenceTimer.Start();
        _heartbeatTimer.Start();
        _reconcileTimer.Start();
    }

    private async Task LoadRoomsAsync()
    {
        try
        {
            var rooms = await _api.GetRoomsAsync();

            // Non-destructive merge: only append rooms we don't already have.
            // This keeps the current selection (and avoids ListBox flicker)
            // while still surfacing rooms created by other users.
            foreach (var r in rooms)
            {
                if (Rooms.All(x => x.Id != r.Id))
                    Rooms.Add(r);
            }
        }
        catch (Exception ex)
        {
            Status = "Cannot load rooms: " + ex.Message;
        }
    }

    private async Task SwitchRoomAsync()
    {
        CancelEdit();
        Messages.Clear();
        OnlineUsers.Clear();
        TypingIndicator = "";
        _lastMessageId = 0;
        _firstMessageId = 0;
        UnreadCount = 0;
        Status = "";

        if (SelectedRoom == null) return;

        await PollMessagesAsync(playSound: false); // don't beep when opening a room
        await PollPresenceAsync();
        await HeartbeatAsync();
    }

    // ---------- Polling ----------

    private async Task PollMessagesAsync(bool playSound = true)
    {
        if (_pollingMessages || SelectedRoom == null) return;
        _pollingMessages = true;
        try
        {
            int roomId = SelectedRoom.Id;
            var msgs = await _api.GetMessagesAsync(roomId, _lastMessageId);

            // Room may have changed while awaiting.
            if (SelectedRoom?.Id != roomId) return;

            bool mineIncluded = false;
            int incomingCount = 0;
            int firstAddedId = 0;
            foreach (var m in msgs)
            {
                if (m.Id <= _lastMessageId) continue;
                bool mine = m.UserId == _api.UserId;
                if (mine) mineIncluded = true;
                else incomingCount++;
                if (firstAddedId == 0) firstAddedId = m.Id;

                AppendMessage(new MessageItem
                {
                    Id = m.Id,
                    Username = m.Username,
                    Content = m.Content,
                    SentUtc = m.SentUtc,
                    IsMine = mine,
                    EditedUtc = m.EditedUtc,
                    AttachmentName = m.AttachmentName,
                    AttachmentUrl = m.AttachmentUrl
                });
                _lastMessageId = m.Id;
            }

            // Remember the oldest loaded id so we can page further back.
            if (_firstMessageId == 0 && firstAddedId != 0)
                _firstMessageId = firstAddedId;

            if (firstAddedId != 0)
            {
                // Force scroll on room open (playSound==false) or on our own message.
                bool force = !playSound || mineIncluded;
                MessagesAppended?.Invoke(incomingCount, force);
                if (playSound && incomingCount > 0)
                    SystemSounds.Asterisk.Play(); // new-message sound
            }
            Status = ""; // connection is healthy
        }
        catch (Exception ex)
        {
            Status = "Offline – reconnecting… (" + ex.Message + ")";
        }
        finally
        {
            _pollingMessages = false;
        }
    }

    /// <summary>Loads the previous page of messages and prepends it (scroll-up history).</summary>
    public async Task LoadOlderAsync()
    {
        if (_loadingOlder || SelectedRoom == null || _firstMessageId <= 1) return;
        _loadingOlder = true;
        try
        {
            int roomId = SelectedRoom.Id;
            var older = await _api.GetOlderMessagesAsync(roomId, _firstMessageId);
            if (SelectedRoom?.Id != roomId) return;

            var items = older
                .Where(m => m.Id < _firstMessageId)
                .Select(m => new MessageItem
                {
                    Id = m.Id,
                    Username = m.Username,
                    Content = m.Content,
                    SentUtc = m.SentUtc,
                    IsMine = m.UserId == _api.UserId,
                    EditedUtc = m.EditedUtc,
                    AttachmentName = m.AttachmentName,
                    AttachmentUrl = m.AttachmentUrl
                })
                .ToList();

            if (items.Count == 0) return;

            PrependOlder(items);
            _firstMessageId = items[0].Id; // items are ascending; [0] is the oldest
            HistoryPrepended?.Invoke();
        }
        catch { /* best effort */ }
        finally
        {
            _loadingOlder = false;
        }
    }

    // ---------- Message list building (with date separators) ----------

    private MessageItem? LastMessage()
    {
        for (int i = Messages.Count - 1; i >= 0; i--)
            if (Messages[i] is MessageItem mi) return mi;
        return null;
    }

    /// <summary>Appends one message, inserting a date divider when the day changes.</summary>
    private void AppendMessage(MessageItem m)
    {
        var last = LastMessage();
        if (last == null || last.LocalDate != m.LocalDate)
            Messages.Add(new DateSeparator { Date = m.LocalDate });
        Messages.Add(m);
    }

    /// <summary>Prepends a batch of older messages (ascending) with date dividers.</summary>
    private void PrependOlder(List<MessageItem> older)
    {
        if (older.Count == 0) return;

        var prefix = new List<object>();
        DateTime? last = null;
        foreach (var m in older)
        {
            if (last == null || last.Value != m.LocalDate)
                prefix.Add(new DateSeparator { Date = m.LocalDate });
            prefix.Add(m);
            last = m.LocalDate;
        }

        // Drop a duplicate divider at the boundary if the existing list starts
        // with a separator for the same day as the last older message.
        if (Messages.Count > 0 && Messages[0] is DateSeparator bsep && older[^1].LocalDate == bsep.Date)
            Messages.RemoveAt(0);

        for (int i = prefix.Count - 1; i >= 0; i--)
            Messages.Insert(0, prefix[i]);
    }

    private async Task PollPresenceAsync()
    {
        if (_pollingPresence || SelectedRoom == null) return;
        _pollingPresence = true;
        try
        {
            int roomId = SelectedRoom.Id;
            var p = await _api.GetPresenceAsync(roomId);
            if (SelectedRoom?.Id != roomId) return;

            OnlineUsers.Clear();
            foreach (var u in p.Online) OnlineUsers.Add(u.Username);

            TypingIndicator = p.Typing.Count switch
            {
                0 => "",
                1 => $"{p.Typing[0]} is typing…",
                2 => $"{p.Typing[0]} and {p.Typing[1]} are typing…",
                _ => "Several people are typing…"
            };
        }
        catch { /* presence is best-effort */ }
        finally
        {
            _pollingPresence = false;
        }
    }

    private async Task HeartbeatAsync()
    {
        if (SelectedRoom == null) return;
        await _api.HeartbeatAsync(SelectedRoom.Id);
    }

    private async Task NotifyTypingAsync()
    {
        if (SelectedRoom == null || string.IsNullOrEmpty(MessageText)) return;
        if ((DateTime.UtcNow - _lastTypingSent).TotalSeconds < 2) return;
        _lastTypingSent = DateTime.UtcNow;
        await _api.TypingAsync(SelectedRoom.Id);
    }

    // ---------- Commands ----------

    private async Task SendAsync()
    {
        if (SelectedRoom == null) return;
        var text = MessageText.Trim();
        if (text.Length == 0) return;

        int roomId = SelectedRoom.Id;

        // Editing an existing message?
        if (_editing != null)
        {
            var target = _editing;
            MessageText = "";
            CancelEdit();
            var updated = await _api.EditMessageAsync(roomId, target.Id, text);
            if (updated != null)
            {
                target.Content = updated.Content;
                target.EditedUtc = updated.EditedUtc;
            }
            else
            {
                Status = "Failed to edit message.";
            }
            return;
        }

        MessageText = "";
        var sent = await _api.SendMessageAsync(roomId, text);
        if (sent == null)
        {
            Status = "Failed to send message.";
            return;
        }
        // Pick it (and anything else new) up immediately.
        await PollMessagesAsync();
    }

    public async Task SendAttachmentAsync(string filePath)
    {
        if (SelectedRoom == null) return;
        int roomId = SelectedRoom.Id;
        var sent = await _api.UploadAttachmentAsync(roomId, filePath);
        if (sent == null)
        {
            Status = "Failed to upload attachment.";
            return;
        }
        await PollMessagesAsync();
    }

    private void BeginEdit(MessageItem? item)
    {
        if (item == null || !item.IsMine || item.HasAttachment) return;
        _editing = item;
        MessageText = item.Content;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(SendButtonText));
    }

    public void CancelEdit()
    {
        if (_editing == null) return;
        _editing = null;
        MessageText = "";
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(SendButtonText));
    }

    private async Task ReconcileRecentAsync()
    {
        if (SelectedRoom == null) return;
        try
        {
            int roomId = SelectedRoom.Id;
            var latest = await _api.GetMessagesAsync(roomId, 0); // latest page
            if (SelectedRoom?.Id != roomId) return;

            foreach (var m in latest)
            {
                var item = FindMessage(m.Id);
                if (item == null) continue;
                if (item.Content != m.Content) item.Content = m.Content;
                if (item.EditedUtc != m.EditedUtc) item.EditedUtc = m.EditedUtc;
            }
        }
        catch { /* best effort */ }
    }

    private MessageItem? FindMessage(int id)
    {
        foreach (var o in Messages)
            if (o is MessageItem mi && mi.Id == id) return mi;
        return null;
    }

    private async Task LoadDmOverviewAsync()
    {
        try
        {
            var list = await _api.GetDmOverviewAsync();
            foreach (var dto in list)
            {
                var existing = DmUsers.FirstOrDefault(x => x.UserId == dto.UserId);
                if (existing == null)
                    DmUsers.Add(new DmUserItem { UserId = dto.UserId, Name = dto.Name, Unread = dto.Unread });
                else
                    existing.Unread = dto.Unread;
            }
        }
        catch { /* best effort */ }
    }

    private async Task CreateRoomAsync()
    {
        var name = NewRoomName.Trim();
        if (name.Length == 0) return;

        var created = await _api.CreateRoomAsync(name);
        NewRoomName = "";
        await LoadRoomsAsync();

        if (created != null)
        {
            var match = Rooms.FirstOrDefault(r => r.Id == created.Id);
            if (match != null) SelectedRoom = match;
        }
        else
        {
            Status = "Could not create room (maybe it already exists).";
        }
    }

    private async Task LogoutAsync()
    {
        _messageTimer.Stop();
        _presenceTimer.Stop();
        _heartbeatTimer.Stop();
        _reconcileTimer.Stop();
        await _api.LogoutAsync();
        LoggedOut?.Invoke();
    }
}
