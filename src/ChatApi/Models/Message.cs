namespace ChatApi.Models;

public class Message
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Content { get; set; } = "";
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the message has been edited.</summary>
    public DateTime? EditedUtc { get; set; }

    // Optional attachment (image or file).
    public string? AttachmentName { get; set; }
    public string? AttachmentUrl { get; set; }
}
