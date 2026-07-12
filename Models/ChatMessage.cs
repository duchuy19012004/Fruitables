using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ChatMessage
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Role { get; set; } = "user"; // user | assistant

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? MetaJson { get; set; }
}
