using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api/rooms/{roomId:int}/messages")]
public class MessagesController : ApiControllerBase
{
    private const int PageSize = 50;

    public MessagesController(AppDbContext db) : base(db) { }

    /// <summary>
    /// Returns messages for a room (always chronological / ascending by Id):
    ///  - beforeId &gt; 0 : the page of OLDER messages with Id &lt; beforeId
    ///                     (used for "load older history" when scrolling up).
    ///  - afterId  &gt; 0 : only NEWER messages with Id &gt; afterId
    ///                     (used by the real-time poll).
    ///  - neither      : the latest page.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(
        int roomId, [FromQuery] int afterId = 0, [FromQuery] int beforeId = 0)
    {
        if (await GetCurrentUserAsync() is null) return Unauthorized();

        var roomMessages = Db.Messages.Where(m => m.RoomId == roomId);

        if (beforeId > 0)
        {
            return await roomMessages
                .Where(m => m.Id < beforeId)
                .OrderByDescending(m => m.Id)
                .Take(PageSize)
                .OrderBy(m => m.Id)
                .Select(m => new MessageDto(
                    m.Id, m.RoomId, m.UserId, m.User!.DisplayName ?? m.User!.Username,
                    m.Content, m.SentUtc, m.EditedUtc, m.AttachmentName, m.AttachmentUrl))
                .ToListAsync();
        }

        if (afterId <= 0)
        {
            return await roomMessages
                .OrderByDescending(m => m.Id)
                .Take(PageSize)
                .OrderBy(m => m.Id)
                .Select(m => new MessageDto(
                    m.Id, m.RoomId, m.UserId, m.User!.DisplayName ?? m.User!.Username,
                    m.Content, m.SentUtc, m.EditedUtc, m.AttachmentName, m.AttachmentUrl))
                .ToListAsync();
        }

        return await roomMessages
            .Where(m => m.Id > afterId)
            .OrderBy(m => m.Id)
            .Select(m => new MessageDto(
                m.Id, m.RoomId, m.UserId, m.User!.DisplayName ?? m.User!.Username,
                m.Content, m.SentUtc, m.EditedUtc, m.AttachmentName, m.AttachmentUrl))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> SendMessage(int roomId, SendMessageRequest req)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (!await Db.Rooms.AnyAsync(r => r.Id == roomId))
            return NotFound(new { error = "Room not found." });

        var content = (req.Content ?? "").Trim();
        if (content.Length == 0)
            return BadRequest(new { error = "Message cannot be empty." });
        if (content.Length > 2000)
            content = content[..2000];

        var msg = new Message
        {
            RoomId = roomId,
            UserId = user.Id,
            Content = content,
            SentUtc = DateTime.UtcNow
        };
        Db.Messages.Add(msg);

        // Sending a message also counts as presence + clears "typing".
        user.LastSeenUtc = DateTime.UtcNow;
        user.CurrentRoomId = roomId;
        user.TypingUntilUtc = null;

        await Db.SaveChangesAsync();

        return new MessageDto(msg.Id, msg.RoomId, user.Id, user.DisplayName ?? user.Username,
            msg.Content, msg.SentUtc, msg.EditedUtc, msg.AttachmentName, msg.AttachmentUrl);
    }

    [HttpPut("{messageId:int}")]
    public async Task<ActionResult<MessageDto>> EditMessage(int roomId, int messageId, EditMessageRequest req)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var msg = await Db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.RoomId == roomId);
        if (msg is null) return NotFound(new { error = "Message not found." });
        if (msg.UserId != user.Id)
            return StatusCode(403, new { error = "You can only edit your own messages." });

        var content = (req.Content ?? "").Trim();
        if (content.Length == 0)
            return BadRequest(new { error = "Message cannot be empty." });
        if (content.Length > 2000)
            content = content[..2000];

        msg.Content = content;
        msg.EditedUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return new MessageDto(msg.Id, msg.RoomId, user.Id, user.DisplayName ?? user.Username,
            msg.Content, msg.SentUtc, msg.EditedUtc, msg.AttachmentName, msg.AttachmentUrl);
    }
}
