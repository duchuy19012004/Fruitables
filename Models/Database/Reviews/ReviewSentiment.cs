using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

// Nhãn cảm xúc tổng của 1 review.
public enum SentimentLabel
{
    Positive = 0,   // Tích cực
    Neutral = 1,    // Trung tính
    Negative = 2,   // Tiêu cực
    Failed = 3      // Không phân tích được (LLM lỗi / JSON hỏng) — admin có thể bấm phân tích lại
}

// Nguồn gán nhãn cảm xúc.
public enum SentimentSource
{
    AiModel = 0,        // LLM phân tích từ comment
    RatingFallback = 1, // Review không có chữ → suy từ số sao
    AdminOverride = 2   // Admin sửa tay
}

// Trạng thái cảnh báo khi review tiêu cực nghiêm trọng.
public enum SentimentAlertStatus
{
    None = 0,           // Không có cảnh báo
    Pending = 1,        // Chưa xử lý
    Acknowledged = 2    // Admin đã xác nhận
}

/// <summary>
/// Kết quả phân tích cảm xúc của 1 review (1-1 với Review).
/// </summary>
public class ReviewSentiment
{
    public int Id { get; set; }

    public int ReviewId { get; set; }

    // Nhãn suy ra cố định từ số sao, độc lập với kết quả LLM.
    public SentimentLabel RatingSentiment { get; set; } = SentimentLabel.Neutral;

    // Nhãn cảm xúc của comment do LLM phân tích; null khi review không có comment.
    public SentimentLabel? CommentSentiment { get; set; }

    // Rating và comment thể hiện hai hướng cảm xúc khác nhau.
    public bool HasRatingCommentConflict { get; set; }

    // Conflict/safety/LLM failure phải được admin xử lý trước khi dùng downstream.
    public bool NeedsManualReview { get; set; }

    // Vấn đề có thể liên quan đến an toàn thực phẩm hoặc sức khỏe.
    public bool HasSafetyRisk { get; set; }

    // Nhãn vận hành: comment rõ ràng thì theo comment, comment trung lập thì theo rating.
    public SentimentLabel Sentiment { get; set; }

    // Mức độ tiêu cực 1-3 (null nếu không phải tiêu cực). >= SevereThreshold → cảnh báo admin.
    public int? Severity { get; set; }

    // Độ tin cậy 0..1 do LLM trả về (rating fallback = 1).
    public float? Confidence { get; set; }

    // Lý do ngắn LLM giải thích (hiển thị cho admin).
    [MaxLength(500)]
    public string? Reason { get; set; }

    public SentimentSource Source { get; set; }

    public DateTime? AnalyzedAtUtc { get; set; }

    // Dùng để nhận biết kết quả được tạo bởi prompt/rule version nào.
    [MaxLength(50)]
    public string? AnalysisVersion { get; set; } = "sentiment-v2";

    // Admin override
    public int? AdminOverrideById { get; set; }
    public DateTime? AdminOverrideAtUtc { get; set; }
    [MaxLength(500)]
    public string? AdminReviewNote { get; set; }

    // Cảnh báo tiêu cực nghiêm trọng
    public SentimentAlertStatus AlertStatus { get; set; } = SentimentAlertStatus.None;
    public int? AcknowledgedById { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }

    public virtual Review Review { get; set; } = null!;
    public virtual User? AdminOverrideBy { get; set; }
    public virtual User? AcknowledgedBy { get; set; }
    public virtual ICollection<ReviewSentimentAspect> Aspects { get; set; } = new List<ReviewSentimentAspect>();
}
