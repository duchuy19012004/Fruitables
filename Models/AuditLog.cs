using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class AuditLog
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }
    public int ChangedByAdminId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
