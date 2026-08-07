namespace Fruitables.Models;

public class ReviewReport
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public int ReportedByUserId { get; set; }

    public ReportReason Reason { get; set; }
    public string? Description { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public int? HandledByAdminId { get; set; }
    public DateTime? HandledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Review Review { get; set; } = null!;
    public virtual User ReportedByUser { get; set; } = null!;
    public virtual User? HandledByAdmin { get; set; }
}

public enum ReportReason
{
    Spam = 0,
    Offensive = 1,
    Fake = 2,
    Other = 3
}

public enum ReportStatus
{
    Pending = 0,
    Resolved = 1,
    Dismissed = 2
}
