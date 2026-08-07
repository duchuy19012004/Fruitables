using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required, MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [NotMapped]
    public string StorageKey { get; set; } = string.Empty;

    public bool IsPrimary { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    // Navigation property
    public virtual Product Product { get; set; } = null!;
}
