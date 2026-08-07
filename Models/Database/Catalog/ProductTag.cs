using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ProductTag
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    // Navigation property
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
