using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Review
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int UserId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    // Status and moderation
    public ReviewStatus Status { get; set; } = ReviewStatus.Approved;
    public bool IsHidden { get; set; } = false;
    public string? HiddenReason { get; set; }
    public int? HiddenByAdminId { get; set; }
    public DateTime? HiddenAt { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public int? DeletedByAdminId { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Additional features
    public bool IsVerifiedPurchase { get; set; } = false;
    public int HelpfulCount { get; set; } = 0;
    public int ReportCount { get; set; } = 0;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string MetadataJson { get; set; } = "{ \"schemaVersion\": 1 }";

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual User? HiddenByAdmin { get; set; }
    public virtual User? DeletedByAdmin { get; set; }
    public virtual ICollection<ReviewReport> Reports { get; set; } = new List<ReviewReport>();
    public virtual ICollection<ReviewHelpful> HelpfulVotes { get; set; } = new List<ReviewHelpful>();
    public virtual ReviewSentiment? Sentiment { get; set; }
}

public enum ReviewStatus
{
    Pending = 0,      // Chờ duyệt
    Approved = 1,     // Đã duyệt
    Rejected = 2      // Từ chối
}
