using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ProductLog
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int AdminId { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Product? Product { get; set; }
    public virtual User Admin { get; set; } = null!;
}

public static class ProductLogActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string SoftDelete = "SoftDelete";
    public const string Restore = "Restore";
    public const string HardDelete = "HardDelete";
    public const string ImageUpload = "ImageUpload";
    public const string ImageDelete = "ImageDelete";
    public const string TagUpdate = "TagUpdate";
    public const string VariantCreate = "VariantCreate";
    public const string VariantUpdate = "VariantUpdate";
    public const string VariantDelete = "VariantDelete";
    public const string Error = "Error";
}
