using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Promotion
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(50)]
    public string? CustomerCode { get; set; }

    public string PayloadJson { get; set; } = "{ \"schemaVersion\": 1 }";

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    public int Revision { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }
}
