namespace ChatApi.Dtos;

// Requests
public record AuthRequest(string Username, string Password);
public record CreateRoomRequest(string Name);
public record SendMessageRequest(string Content);
public record EditMessageRequest(string Content);
public record HeartbeatRequest(int RoomId);
public record TypingRequest(int RoomId);
public record UpdateProfileRequest(string? DisplayName, string? NewPassword);
public record SendDmRequest(string Content);

// Responses
public record AuthResponse(int UserId, string Username, string Token, string? DisplayName);
public record ProfileResponse(int UserId, string Username, string? DisplayName);
public record RoomDto(int Id, string Name);
public record MessageDto(
    int Id, int RoomId, int UserId, string Username, string Content, DateTime SentUtc,
    DateTime? EditedUtc, string? AttachmentName, string? AttachmentUrl);
public record PresenceUser(int UserId, string Username);
public record PresenceDto(List<PresenceUser> Online, List<string> Typing);

// Direct messages
public record DirectMessageDto(
    int Id, int SenderId, int RecipientId, string Content, DateTime SentUtc,
    DateTime? DeliveredUtc, DateTime? ReadUtc, bool Mine);
public record DmUserDto(int UserId, string Name, int Unread);
