using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChatClient.Models;

namespace ChatClient.Services;

/// <summary>
/// Thin HTTP wrapper around the ChatApi REST endpoints. Holds the session token
/// and current user identity after a successful login/register.
/// </summary>
public class ChatApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public int UserId { get; private set; }
    public string Username { get; private set; } = "";
    public string? DisplayName { get; private set; }
    public string? Token { get; private set; }

    public ChatApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    // ---------- Auth ----------

    public Task<(bool ok, string? error)> RegisterAsync(string username, string password)
        => AuthAsync("api/auth/register", username, password);

    public Task<(bool ok, string? error)> LoginAsync(string username, string password)
        => AuthAsync("api/auth/login", username, password);

    private async Task<(bool ok, string? error)> AuthAsync(string url, string username, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(url, new { username, password });
            if (!resp.IsSuccessStatusCode)
                return (false, await ReadErrorAsync(resp));

            var data = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
            if (data is null) return (false, "Invalid server response.");

            UserId = data.UserId;
            Username = data.Username;
            DisplayName = data.DisplayName;
            Token = data.Token;
            ApplyAuthHeader();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, "Cannot reach server: " + ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); }
        catch { /* ignore */ }
        Token = null;
        ApplyAuthHeader();
    }

    public async Task<(bool ok, string? error)> UpdateProfileAsync(string? displayName, string? newPassword)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync("api/auth/profile", new { displayName, newPassword });
            if (!resp.IsSuccessStatusCode)
                return (false, await ReadErrorAsync(resp));

            var data = await resp.Content.ReadFromJsonAsync<ProfileResponse>(JsonOpts);
            if (data != null) DisplayName = data.DisplayName;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void ApplyAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization =
            Token is null ? null : new AuthenticationHeaderValue("Bearer", Token);
    }

    // ---------- Rooms ----------

    public async Task<List<RoomDto>> GetRoomsAsync()
        => await _http.GetFromJsonAsync<List<RoomDto>>("api/rooms", JsonOpts) ?? new();

    public async Task<RoomDto?> CreateRoomAsync(string name)
    {
        var resp = await _http.PostAsJsonAsync("api/rooms", new { name });
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RoomDto>(JsonOpts)
            : null;
    }

    // ---------- Messages ----------

    public async Task<List<MessageDto>> GetMessagesAsync(int roomId, int afterId)
        => await _http.GetFromJsonAsync<List<MessageDto>>(
               $"api/rooms/{roomId}/messages?afterId={afterId}", JsonOpts) ?? new();

    /// <summary>Loads the page of older messages with Id &lt; beforeId (history).</summary>
    public async Task<List<MessageDto>> GetOlderMessagesAsync(int roomId, int beforeId)
        => await _http.GetFromJsonAsync<List<MessageDto>>(
               $"api/rooms/{roomId}/messages?beforeId={beforeId}", JsonOpts) ?? new();

    public async Task<MessageDto?> SendMessageAsync(int roomId, string content)
    {
        var resp = await _http.PostAsJsonAsync($"api/rooms/{roomId}/messages", new { content });
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MessageDto>(JsonOpts)
            : null;
    }

    public async Task<MessageDto?> EditMessageAsync(int roomId, int messageId, string content)
    {
        var resp = await _http.PutAsJsonAsync($"api/rooms/{roomId}/messages/{messageId}", new { content });
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MessageDto>(JsonOpts)
            : null;
    }

    public async Task<MessageDto?> UploadAttachmentAsync(int roomId, string filePath)
    {
        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(bytes);
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var resp = await _http.PostAsync($"api/rooms/{roomId}/attachments", form);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MessageDto>(JsonOpts)
            : null;
    }

    // ---------- Direct messages ----------

    public async Task<List<DmUserDto>> GetDmOverviewAsync()
        => await _http.GetFromJsonAsync<List<DmUserDto>>("api/dm/overview", JsonOpts) ?? new();

    public async Task<List<DirectMessageDto>> GetDmMessagesAsync(int otherId, int afterId)
        => await _http.GetFromJsonAsync<List<DirectMessageDto>>(
               $"api/dm/{otherId}/messages?afterId={afterId}", JsonOpts) ?? new();

    public async Task<DirectMessageDto?> SendDmAsync(int otherId, string content)
    {
        var resp = await _http.PostAsJsonAsync($"api/dm/{otherId}", new { content });
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<DirectMessageDto>(JsonOpts)
            : null;
    }

    public async Task MarkDmReadAsync(int otherId)
    {
        try { await _http.PostAsync($"api/dm/{otherId}/read", null); }
        catch { /* best effort */ }
    }

    // ---------- Presence ----------

    public async Task HeartbeatAsync(int roomId)
    {
        try { await _http.PostAsJsonAsync("api/presence/heartbeat", new { roomId }); }
        catch { /* best effort */ }
    }

    public async Task TypingAsync(int roomId)
    {
        try { await _http.PostAsJsonAsync("api/presence/typing", new { roomId }); }
        catch { /* best effort */ }
    }

    public async Task<PresenceDto> GetPresenceAsync(int roomId)
        => await _http.GetFromJsonAsync<PresenceDto>(
               $"api/rooms/{roomId}/presence", JsonOpts) ?? new(new(), new());

    // ---------- Helpers ----------

    private static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (doc.ValueKind == JsonValueKind.Object &&
                doc.TryGetProperty("error", out var e) &&
                e.ValueKind == JsonValueKind.String)
            {
                return e.GetString()!;
            }
        }
        catch { /* fall through */ }
        return resp.ReasonPhrase ?? $"HTTP {(int)resp.StatusCode}";
    }
}
