namespace ChatClient.ViewModels;

/// <summary>A day divider shown in the message list between messages of different dates.</summary>
public class DateSeparator
{
    public DateTime Date { get; init; }

    public string Text
    {
        get
        {
            var today = DateTime.Now.Date;
            if (Date == today) return "Today";
            if (Date == today.AddDays(-1)) return "Yesterday";
            return Date.ToString("dddd, d MMMM yyyy");
        }
    }
}
