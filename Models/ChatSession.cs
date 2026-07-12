using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ChatSession
{
    public Guid Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string? Source { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
