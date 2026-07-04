using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api/rooms")]
public class RoomsController : ApiControllerBase
{
    public RoomsController(AppDbContext db) : base(db) { }

    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> GetRooms()
    {
        if (await GetCurrentUserAsync() is null) return Unauthorized();

        return await Db.Rooms
            .OrderBy(r => r.Id)
            .Select(r => new RoomDto(r.Id, r.Name))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<RoomDto>> CreateRoom(CreateRoomRequest req)
    {
        if (await GetCurrentUserAsync() is null) return Unauthorized();

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new { error = "Room name is required." });
        if (name.Length > 64)
            name = name[..64];
        if (await Db.Rooms.AnyAsync(r => r.Name == name))
            return Conflict(new { error = "A room with that name already exists." });

        var room = new Room { Name = name, CreatedUtc = DateTime.UtcNow };
        Db.Rooms.Add(room);
        await Db.SaveChangesAsync();

        return new RoomDto(room.Id, room.Name);
    }
}
