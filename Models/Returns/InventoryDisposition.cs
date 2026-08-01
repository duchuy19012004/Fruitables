using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class InventoryDisposition
{
    public int Id { get; set; }
    public int ReturnRequestItemId { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "decimal(12,3)")] public decimal QuantityKg { get; set; }
    public InventoryDispositionType Disposition { get; set; }
    public int InspectorUserId { get; set; }
    [Required, MaxLength(1000)] public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public ReturnRequestItem ReturnRequestItem { get; set; } = null!;
    public User InspectorUser { get; set; } = null!;
}
