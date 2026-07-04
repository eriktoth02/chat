using System.Windows;
using System.Windows.Media;

namespace ChatClient.ViewModels;

/// <summary>A chat message prepared for display (observable so edits update live).</summary>
public class MessageItem : ViewModelBase
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public DateTime SentUtc { get; init; }
    public bool IsMine { get; init; }

    public string? AttachmentName { get; init; }
    public string? AttachmentUrl { get; init; }

    private string _content = "";
    public string Content
    {
        get => _content;
        set { if (Set(ref _content, value)) OnPropertyChanged(nameof(HasText)); }
    }

    private DateTime? _editedUtc;
    public DateTime? EditedUtc
    {
        get => _editedUtc;
        set { if (Set(ref _editedUtc, value)) OnPropertyChanged(nameof(Header)); }
    }

    public bool HasText => !string.IsNullOrEmpty(_content);
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentUrl);
    public bool IsImage => HasAttachment && IsImageName(AttachmentName);
    public bool IsFile => HasAttachment && !IsImage;

    public string Header =>
        $"{Username} · {SentUtc.ToLocalTime():HH:mm}{(EditedUtc != null ? "  (edited)" : "")}";

    public DateTime LocalDate => SentUtc.ToLocalTime().Date;

    public HorizontalAlignment Align => IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public Brush BubbleBrush => IsMine
        ? new SolidColorBrush(Color.FromRgb(0x89, 0x4F, 0xD6))  // purple (mine)
        : new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)); // gray (others)

    private static bool IsImageName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }
}
