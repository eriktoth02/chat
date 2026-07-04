namespace ChatApi.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";

    /// <summary>Optional friendly name shown instead of Username. Null = use Username.</summary>
    public string? DisplayName { get; set; }

    public string PasswordHash { get; set; } = "";

    /// <summary>Session token issued on login/register. Null when logged out.</summary>
    public string? Token { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // --- Presence ---
    public DateTime LastSeenUtc { get; set; }
    public int? CurrentRoomId { get; set; }
    public DateTime? TypingUntilUtc { get; set; }
}
