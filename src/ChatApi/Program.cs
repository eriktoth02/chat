using System.Data;
using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "server=localhost;port=3306;database=chatapp;user=root;password=root";

// A fixed server version is used (instead of AutoDetect) so the app does not
// need to connect before the 'chatapp' database exists - EnsureCreated() below
// then creates the database and tables on first run.
// MariaDB users: replace with new MariaDbServerVersion(new Version(10, 11)).
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Allow the WPF client (any local origin) to call the API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// --- Create database/tables if missing and seed a default room ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Lightweight schema upgrades for DBs created by an earlier version.
    // (EnsureCreated does not alter existing tables, so add new columns here.)
    EnsureColumn(db, "Users", "DisplayName", "ALTER TABLE `Users` ADD COLUMN `DisplayName` VARCHAR(64) NULL");
    EnsureColumn(db, "Messages", "EditedUtc", "ALTER TABLE `Messages` ADD COLUMN `EditedUtc` DATETIME(6) NULL");
    EnsureColumn(db, "Messages", "AttachmentName", "ALTER TABLE `Messages` ADD COLUMN `AttachmentName` VARCHAR(260) NULL");
    EnsureColumn(db, "Messages", "AttachmentUrl", "ALTER TABLE `Messages` ADD COLUMN `AttachmentUrl` VARCHAR(400) NULL");

    // Direct-messages table (EnsureCreated won't add it to an existing database).
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS `DirectMessages` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `SenderId` int NOT NULL,
            `RecipientId` int NOT NULL,
            `Content` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
            `SentUtc` datetime(6) NOT NULL,
            `DeliveredUtc` datetime(6) NULL,
            `ReadUtc` datetime(6) NULL,
            CONSTRAINT `PK_DirectMessages` PRIMARY KEY (`Id`),
            KEY `IX_DM_Pair` (`SenderId`,`RecipientId`,`Id`),
            KEY `IX_DM_Recipient_Read` (`RecipientId`,`ReadUtc`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

    if (!db.Rooms.Any())
    {
        db.Rooms.Add(new Room { Name = "General" });
        db.SaveChanges();
    }
}

// Serve uploaded attachment files from wwwroot (e.g. /uploads/xxx).
var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot)
});

// --- Pipeline ---
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();

// Adds a column only if it does not already exist (idempotent, MySQL-safe).
static void EnsureColumn(AppDbContext db, string table, string column, string alterSql)
{
    var conn = db.Database.GetDbConnection();
    bool opened = false;
    if (conn.State != ConnectionState.Open) { conn.Open(); opened = true; }
    try
    {
        using var check = conn.CreateCommand();
        check.CommandText =
            "SELECT COUNT(*) FROM information_schema.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c";
        AddParam(check, "@t", table);
        AddParam(check, "@c", column);
        var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
        if (!exists)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = alterSql;
            alter.ExecuteNonQuery();
        }
    }
    finally
    {
        if (opened) conn.Close();
    }

    static void AddParam(System.Data.Common.DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
