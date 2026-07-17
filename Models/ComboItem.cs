using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ComboItem
{
    public int Id { get; set; }

    public int ComboId { get; set; }

    public int ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;

    public virtual Combo Combo { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
}
