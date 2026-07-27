using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnEvent
{
    public long Id { get; set; }
    public int ReturnRequestId { get; set; }
    public ReturnEventType Type { get; set; }
    public ReturnRequestStatus? FromStatus { get; set; }
    public ReturnRequestStatus? ToStatus { get; set; }
    public int? ActorUserId { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(4000)] public string? DataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public User? ActorUser { get; set; }
}
