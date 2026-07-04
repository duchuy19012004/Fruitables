using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Returned = 5
}

public enum PaymentMethod
{
    BankTransfer,
    Check,
    COD,
    Paypal
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Refunded
}

public enum ShippingMethod
{
    Free,
    FlatRate,
    LocalPickup
}

public class Order
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    // Reference to selected address
    public int? AddressId { get; set; }

    // JSON snapshot of address at order time
    [MaxLength(2000)]
    public string? ShippingSnapshot { get; set; }

    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ShippingFee { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Discount { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Total { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [MaxLength(16)]
    public string? PaymentCode { get; set; }

    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.FlatRate;

    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Address? Address { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public virtual ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public virtual ICollection<OrderNote> OrderNotes { get; set; } = new List<OrderNote>();
}
