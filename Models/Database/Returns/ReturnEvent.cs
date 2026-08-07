using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnEvent
{
    public long Id { get; set; }

    public int ReturnRequestId { get; set; }
    public int? ReturnRequestItemId { get; set; }
    public ReturnRequestStatus? OldStatus { get; set; }
    public ReturnRequestStatus? NewStatus { get; set; }
    public ReturnEventType EventType { get; set; }
    public int? ActorUserId { get; set; }

    [MaxLength(4000)]
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ReturnRequest ReturnRequest { get; set; } = null!;
    public virtual ReturnRequestItem? ReturnRequestItem { get; set; }
    public virtual User? ActorUser { get; set; }
}
