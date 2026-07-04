using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api/dm")]
public class DirectMessagesController : ApiControllerBase
{
    public DirectMessagesController(AppDbContext db) : base(db) { }

    /// <summary>All other users with the count of unread messages they sent me.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<List<DmUserDto>>> Overview()
    {
        var me = await GetCurrentUserAsync();
        if (me is null) return Unauthorized();

        var users = await Db.Users
            .Where(u => u.Id != me.Id)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.Username })
            .ToListAsync();

        var unreadBySender = await Db.DirectMessages
            .Where(d => d.RecipientId == me.Id && d.ReadUtc == null)
            .GroupBy(d => d.SenderId)
            .Select(g => new { SenderId = g.Key, Count = g.Count() })
            .ToListAsync();

        var map = unreadBySender.ToDictionary(x => x.SenderId, x => x.Count);

        return users
            .Select(u => new DmUserDto(u.Id, u.Name, map.TryGetValue(u.Id, out var c) ? c : 0))
            .OrderByDescending(u => u.Unread)
            .ThenBy(u => u.Name)
            .ToList();
    }

    /// <summary>Conversation between me and another user. Marks incoming as delivered.</summary>
    [HttpGet("{otherId:int}/messages")]
    public async Task<ActionResult<List<DirectMessageDto>>> GetConversation(int otherId, [FromQuery] int afterId = 0)
    {
        var me = await GetCurrentUserAsync();
        if (me is null) return Unauthorized();

        var q = Db.DirectMessages.Where(d =>
            (d.SenderId == me.Id && d.RecipientId == otherId) ||
            (d.SenderId == otherId && d.RecipientId == me.Id));

        if (afterId > 0) q = q.Where(d => d.Id > afterId);

        var list = await q.OrderBy(d => d.Id).Take(200).ToListAsync();

        var now = DateTime.UtcNow;
        bool changed = false;
        foreach (var d in list)
            if (d.RecipientId == me.Id && d.DeliveredUtc == null) { d.DeliveredUtc = now; changed = true; }
        if (changed) await Db.SaveChangesAsync();

        return list.Select(d => new DirectMessageDto(
            d.Id, d.SenderId, d.RecipientId, d.Content, d.SentUtc,
            d.DeliveredUtc, d.ReadUtc, d.SenderId == me.Id)).ToList();
    }

    [HttpPost("{otherId:int}")]
    public async Task<ActionResult<DirectMessageDto>> Send(int otherId, SendDmRequest req)
    {
        var me = await GetCurrentUserAsync();
        if (me is null) return Unauthorized();
        if (!await Db.Users.AnyAsync(u => u.Id == otherId))
            return NotFound(new { error = "User not found." });

        var content = (req.Content ?? "").Trim();
        if (content.Length == 0) return BadRequest(new { error = "Message cannot be empty." });
        if (content.Length > 2000) content = content[..2000];

        var dm = new DirectMessage
        {
            SenderId = me.Id,
            RecipientId = otherId,
            Content = content,
            SentUtc = DateTime.UtcNow
        };
        Db.DirectMessages.Add(dm);
        me.LastSeenUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return new DirectMessageDto(dm.Id, dm.SenderId, dm.RecipientId, dm.Content, dm.SentUtc,
            dm.DeliveredUtc, dm.ReadUtc, true);
    }

    /// <summary>Marks all messages from otherId to me as read.</summary>
    [HttpPost("{otherId:int}/read")]
    public async Task<IActionResult> MarkRead(int otherId)
    {
        var me = await GetCurrentUserAsync();
        if (me is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var unread = await Db.DirectMessages
            .Where(d => d.SenderId == otherId && d.RecipientId == me.Id && d.ReadUtc == null)
            .ToListAsync();

        foreach (var d in unread)
        {
            d.ReadUtc = now;
            d.DeliveredUtc ??= now;
        }
        if (unread.Count > 0) await Db.SaveChangesAsync();
        return NoContent();
    }
}
