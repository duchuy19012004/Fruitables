using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class SearchHotKeyword
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Text { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string NormalizedText { get; set; } = string.Empty;

    /// <summary>Higher = preferred within keyword group.</summary>
    public int Weight { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
