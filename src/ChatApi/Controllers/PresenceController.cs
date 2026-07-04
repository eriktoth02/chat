using ChatApi.Data;
using ChatApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api")]
public class PresenceController : ApiControllerBase
{
    /// <summary>A user counts as "online" if seen within this window.</summary>
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(15);

    public PresenceController(AppDbContext db) : base(db) { }

    /// <summary>Client calls this periodically to report it is alive and which room it is viewing.</summary>
    [HttpPost("presence/heartbeat")]
    public async Task<IActionResult> Heartbeat(HeartbeatRequest req)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.LastSeenUtc = DateTime.UtcNow;
        user.CurrentRoomId = req.RoomId;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Client calls this while the user is typing; sets a short-lived "typing" flag.</summary>
    [HttpPost("presence/typing")]
    public async Task<IActionResult> Typing(TypingRequest req)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.LastSeenUtc = DateTime.UtcNow;
        user.CurrentRoomId = req.RoomId;
        user.TypingUntilUtc = DateTime.UtcNow.AddSeconds(4);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Returns who is online in a room and who is currently typing.</summary>
    [HttpGet("rooms/{roomId:int}/presence")]
    public async Task<ActionResult<PresenceDto>> GetPresence(int roomId)
    {
        var me = await GetCurrentUserAsync();
        if (me is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var since = now - OnlineWindow;

        var users = await Db.Users
            .Where(u => u.CurrentRoomId == roomId && u.LastSeenUtc >= since)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.Username, u.TypingUntilUtc })
            .ToListAsync();

        var online = users
            .OrderBy(u => u.Name)
            .Select(u => new PresenceUser(u.Id, u.Name))
            .ToList();

        var typing = users
            .Where(u => u.Id != me.Id && u.TypingUntilUtc != null && u.TypingUntilUtc > now)
            .OrderBy(u => u.Name)
            .Select(u => u.Name)
            .ToList();

        return new PresenceDto(online, typing);
    }
}
