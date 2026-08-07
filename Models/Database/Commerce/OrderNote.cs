using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class OrderNote
{
    public int Id { get; set; }

    [Required]
    public int OrderId { get; set; }

    [Required]
    public int AdminId { get; set; }

    [Required, MaxLength(100)]
    public string AdminName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    // Navigation
    public virtual Order Order { get; set; } = null!;
}
