using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    public AuthController(AppDbContext db) : base(db) { }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(AuthRequest req)
    {
        var username = (req.Username ?? "").Trim();
        if (username.Length < 3)
            return BadRequest(new { error = "Username must be at least 3 characters." });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 4)
            return BadRequest(new { error = "Password must be at least 4 characters." });

        if (await Db.Users.AnyAsync(u => u.Username == username))
            return Conflict(new { error = "Username is already taken." });

        var user = new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(req.Password),
            Token = NewToken(),
            CreatedUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        return new AuthResponse(user.Id, user.Username, user.Token!, user.DisplayName);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(AuthRequest req)
    {
        var username = (req.Username ?? "").Trim();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !PasswordHasher.Verify(req.Password ?? "", user.PasswordHash))
            return Unauthorized(new { error = "Invalid username or password." });

        user.Token = NewToken();
        user.LastSeenUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return new AuthResponse(user.Id, user.Username, user.Token!, user.DisplayName);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(UpdateProfileRequest req)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (req.DisplayName != null)
        {
            var dn = req.DisplayName.Trim();
            if (dn.Length > 64) dn = dn[..64];
            user.DisplayName = dn.Length == 0 ? null : dn;
        }

        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            if (req.NewPassword.Length < 4)
                return BadRequest(new { error = "Password must be at least 4 characters." });
            user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
        }

        await Db.SaveChangesAsync();
        return new ProfileResponse(user.Id, user.Username, user.DisplayName);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.Token = null;
        user.CurrentRoomId = null;
        user.TypingUntilUtc = null;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");
}
