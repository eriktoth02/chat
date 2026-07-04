using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Token);
            e.Property(u => u.Username).HasMaxLength(64);
            e.Property(u => u.DisplayName).HasMaxLength(64);
        });

        b.Entity<Room>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(64);
        });

        b.Entity<Message>(e =>
        {
            e.HasIndex(m => new { m.RoomId, m.Id });
            e.Property(m => m.Content).HasMaxLength(2000);
            e.Property(m => m.AttachmentName).HasMaxLength(260);
            e.Property(m => m.AttachmentUrl).HasMaxLength(400);
            e.HasOne(m => m.Room).WithMany(r => r.Messages).HasForeignKey(m => m.RoomId);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId);
        });

        b.Entity<DirectMessage>(e =>
        {
            e.HasIndex(d => new { d.SenderId, d.RecipientId, d.Id });
            e.HasIndex(d => new { d.RecipientId, d.ReadUtc });
            e.Property(d => d.Content).HasMaxLength(2000);
        });
    }
}
