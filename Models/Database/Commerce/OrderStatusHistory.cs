using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public OrderStatus OldStatus { get; set; }

    public OrderStatus NewStatus { get; set; }

    public int AdminId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    // Navigation properties
    public virtual Order Order { get; set; } = null!;
    public virtual User Admin { get; set; } = null!;
}
