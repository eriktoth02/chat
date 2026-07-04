namespace ChatClient.ViewModels;

/// <summary>A user in the sidebar "Direct messages" list, with an unread badge.</summary>
public class DmUserItem : ViewModelBase
{
    public int UserId { get; init; }
    public string Name { get; init; } = "";

    private int _unread;
    public int Unread
    {
        get => _unread;
        set
        {
            if (Set(ref _unread, value))
            {
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(Badge));
            }
        }
    }

    public bool HasUnread => _unread > 0;
    public string Badge => _unread > 0 ? _unread.ToString() : "";
}
