using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ComboAuditLog
{
    public long Id { get; set; }
    public int? ComboId { get; set; }
    public int? AdminId { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    public int Revision { get; set; }

    [MaxLength(2000)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Combo? Combo { get; set; }
    public virtual User? Admin { get; set; }
}

public static class ComboAuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Archive = "Archive";
}
