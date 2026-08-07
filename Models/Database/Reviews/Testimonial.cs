using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Testimonial
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Profession { get; set; }

    [MaxLength(255)]
    public string? Avatar { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User? User { get; set; }
}
