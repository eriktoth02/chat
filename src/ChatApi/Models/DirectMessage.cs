namespace ChatApi.Models;

/// <summary>A 1:1 private message between two users (separate from room messages).</summary>
public class DirectMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public string Content { get; set; } = "";
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the recipient's client has fetched the message.</summary>
    public DateTime? DeliveredUtc { get; set; }

    /// <summary>Set when the recipient has opened the conversation.</summary>
    public DateTime? ReadUtc { get; set; }
}
