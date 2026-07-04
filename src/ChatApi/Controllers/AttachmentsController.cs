using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api/rooms/{roomId:int}/attachments")]
public class AttachmentsController : ApiControllerBase
{
    private readonly IWebHostEnvironment _env;
    private const long MaxBytes = 15 * 1024 * 1024; // 15 MB

    public AttachmentsController(AppDbContext db, IWebHostEnvironment env) : base(db) => _env = env;

    [HttpPost]
    public async Task<ActionResult<MessageDto>> Upload(int roomId, IFormFile file)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (!await Db.Rooms.AnyAsync(r => r.Id == roomId))
            return NotFound(new { error = "Room not found." });
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });
        if (file.Length > MaxBytes)
            return BadRequest(new { error = "File too large (max 15 MB)." });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeName = Path.GetFileName(file.FileName);
        var unique = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(uploadsDir, unique);

        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/{Uri.EscapeDataString(unique)}";

        var msg = new Message
        {
            RoomId = roomId,
            UserId = user.Id,
            Content = "",
            SentUtc = DateTime.UtcNow,
            AttachmentName = safeName,
            AttachmentUrl = url
        };
        Db.Messages.Add(msg);
        user.LastSeenUtc = DateTime.UtcNow;
        user.CurrentRoomId = roomId;
        await Db.SaveChangesAsync();

        return new MessageDto(msg.Id, msg.RoomId, user.Id, user.DisplayName ?? user.Username,
            msg.Content, msg.SentUtc, msg.EditedUtc, msg.AttachmentName, msg.AttachmentUrl);
    }
}
