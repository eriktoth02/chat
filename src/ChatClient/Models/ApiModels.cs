namespace ChatClient.Models;

// Mirror of the API DTOs. System.Text.Json is configured case-insensitive
// in ChatApiClient so the API's camelCase JSON binds to these records.
public record AuthResponse(int UserId, string Username, string Token, string? DisplayName);
public record ProfileResponse(int UserId, string Username, string? DisplayName);
public record RoomDto(int Id, string Name);
public record MessageDto(
    int Id, int RoomId, int UserId, string Username, string Content, DateTime SentUtc,
    DateTime? EditedUtc, string? AttachmentName, string? AttachmentUrl);
public record PresenceUser(int UserId, string Username);
public record PresenceDto(List<PresenceUser> Online, List<string> Typing);

public record DirectMessageDto(
    int Id, int SenderId, int RecipientId, string Content, DateTime SentUtc,
    DateTime? DeliveredUtc, DateTime? ReadUtc, bool Mine);
public record DmUserDto(int UserId, string Name, int Unread);
