using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly AppDbContext Db;

    protected ApiControllerBase(AppDbContext db) => Db = db;

    /// <summary>
    /// Resolves the authenticated user from the "Authorization: Bearer {token}" header.
    /// Returns null when the header is missing or the token is unknown.
    /// </summary>
    protected async Task<User?> GetCurrentUserAsync()
    {
        string? auth = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth)) return null;

        string token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth["Bearer ".Length..].Trim()
            : auth.Trim();

        if (string.IsNullOrWhiteSpace(token)) return null;

        return await Db.Users.FirstOrDefaultAsync(u => u.Token == token);
    }
}
