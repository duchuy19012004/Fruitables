using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ContentEntry
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string EntryType { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{ \"schemaVersion\": 1 }";

    public bool IsActive { get; set; } = true;
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }
}
