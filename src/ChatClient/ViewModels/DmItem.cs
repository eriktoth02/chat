using System.Windows;
using System.Windows.Media;

namespace ChatClient.ViewModels;

/// <summary>A single direct message with live delivered/read status.</summary>
public class DmItem : ViewModelBase
{
    public int Id { get; init; }
    public string Content { get; init; } = "";
    public DateTime SentUtc { get; init; }
    public bool Mine { get; init; }

    private DateTime? _deliveredUtc;
    public DateTime? DeliveredUtc
    {
        get => _deliveredUtc;
        set { if (Set(ref _deliveredUtc, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    private DateTime? _readUtc;
    public DateTime? ReadUtc
    {
        get => _readUtc;
        set { if (Set(ref _readUtc, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public string Header => $"{SentUtc.ToLocalTime():HH:mm}";

    public string StatusText =>
        !Mine ? "" :
        _readUtc != null ? "✓✓ Read" :
        _deliveredUtc != null ? "✓ Delivered" : "✓ Sent";

    public Visibility StatusVisibility => Mine ? Visibility.Visible : Visibility.Collapsed;

    public HorizontalAlignment Align => Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public Brush BubbleBrush => Mine
        ? new SolidColorBrush(Color.FromRgb(0x89, 0x4F, 0xD6))
        : new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
}
