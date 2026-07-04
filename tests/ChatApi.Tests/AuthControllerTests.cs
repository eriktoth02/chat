using ChatApi.Controllers;
using ChatApi.Data;
using ChatApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChatApi.Tests;

public class AuthControllerTests
{
    private static AppDbContext NewDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        using var db = NewDb();
        var controller = new AuthController(db);

        var result = await controller.Register(new AuthRequest("alice", "pass"));

        var resp = Assert.IsType<AuthResponse>(result.Value);
        Assert.Equal("alice", resp.Username);
        Assert.False(string.IsNullOrEmpty(resp.Token));
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_Duplicate_ReturnsConflict()
    {
        using var db = NewDb();
        var controller = new AuthController(db);

        await controller.Register(new AuthRequest("bob", "pass"));
        var second = await controller.Register(new AuthRequest("bob", "pass"));

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        using var db = NewDb();
        var controller = new AuthController(db);

        var result = await controller.Register(new AuthRequest("erin", "ab"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        using var db = NewDb();
        var controller = new AuthController(db);

        await controller.Register(new AuthRequest("carol", "pass"));
        var login = await controller.Login(new AuthRequest("carol", "pass"));

        Assert.NotNull(login.Value);
        Assert.Equal("carol", login.Value!.Username);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var db = NewDb();
        var controller = new AuthController(db);

        await controller.Register(new AuthRequest("dave", "pass"));
        var login = await controller.Login(new AuthRequest("dave", "nope"));

        Assert.IsType<UnauthorizedObjectResult>(login.Result);
    }
}
