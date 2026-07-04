namespace ChatApi.Models;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<Message> Messages { get; set; } = new();
}
