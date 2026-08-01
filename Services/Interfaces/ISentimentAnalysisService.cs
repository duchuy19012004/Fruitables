using Fruitables.Models;
using Fruitables.Services.Sentiment;

namespace Fruitables.Services.Interfaces;

public interface ISentimentAnalysisService
{
    // Phân tích 1 batch review (gom ≤ BatchSize/lần gọi LLM). Review không comment → fallback rating.
    Task<int> AnalyzeAsync(IReadOnlyList<int> reviewIds, CancellationToken ct = default);

    // Đếm review chưa phân tích (cho nút backfill trên admin)
    Task<int> CountUnanalyzedAsync(CancellationToken ct = default);

    // Enqueue backfill: chia review chưa phân tích thành chunks vào outbox
    Task<int> EnqueueBackfillAsync(CancellationToken ct = default);

    // Dashboard: phân bố cảm xúc, xu hướng, top khía cạnh, top sản phẩm
    Task<SentimentDashboardData> GetDashboardAsync(CancellationToken ct = default);

    // Danh sách review theo bộ lọc (admin). maxPageSize chặn trần PageSize (mặc định 100; export truyền giá trị lớn hơn).
    Task<PagedSentimentReviews> GetReviewsAsync(SentimentReviewFilter filter, int maxPageSize = 100, CancellationToken ct = default);

    // Admin sửa tay nhãn + severity (ghi audit)
    Task<bool> OverrideAsync(int reviewId, SentimentLabel label, int? severity, string? note, int adminId, CancellationToken ct = default);

    // Admin xác nhận cảnh báo tiêu cực nghiêm trọng
    Task<bool> AcknowledgeAlertAsync(int reviewId, int adminId, CancellationToken ct = default);

    // Đếm cảnh báo chưa xử lý (badge trên admin)
    Task<int> CountPendingAlertsAsync(CancellationToken ct = default);

    // Bối cảnh review: khách hàng + đơn hàng gần nhất chứa sản phẩm (cho CSKH chủ động)
    Task<ReviewContextDto?> GetReviewContextAsync(int reviewId, CancellationToken ct = default);

    // LLM draft phản hồi cho review tiêu cực (admin duyệt/sửa rồi gửi)
    Task<string?> GenerateReplyDraftAsync(int reviewId, CancellationToken ct = default);
}

public sealed class ReviewContextDto
{
    public int ReviewId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? OrderId { get; set; }
    public string? OrderNumber { get; set; }
}

public sealed class SentimentDashboardData
{
    public int TotalAnalyzed { get; set; }
    public int PositiveCount { get; set; }
    public int NeutralCount { get; set; }
    public int NegativeCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingAlertCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ConflictCount { get; set; }
    public int SafetyRiskCount { get; set; }
    public int UnanalyzedCount { get; set; }
    public float NegativeRate { get; set; }       // % tiêu cực
    public List<SentimentTrendPoint> Trend { get; set; } = new();          // 14 ngày gần nhất
    public List<AspectCount> TopNegativeAspects { get; set; } = new();     // khía cạnh bị chê nhiều
    public List<ProductSentiment> TopNegativeProducts { get; set; } = new(); // sản phẩm bị chê nhiều
}

public sealed class SentimentTrendPoint
{
    public string Date { get; set; } = string.Empty; // dd/MM
    public int Positive { get; set; }
    public int Neutral { get; set; }
    public int Negative { get; set; }
}

public sealed class AspectCount
{
    public string Aspect { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ProductSentiment
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int NegativeCount { get; set; }
    public int ConflictCount { get; set; }
    public int TotalCount { get; set; }
    public float NegativeRate { get; set; }
}

public sealed class SentimentReviewFilter
{
    public int? ProductId { get; set; }
    public SentimentLabel? Sentiment { get; set; }
    public int? Severity { get; set; }
    public bool? AlertOnly { get; set; }
    public bool? ConflictOnly { get; set; }
    public bool? NeedsManualReviewOnly { get; set; }
    public bool? SafetyOnly { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PagedSentimentReviews
{
    public List<SentimentReviewRow> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public sealed class SentimentReviewRow
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public SentimentLabel Label { get; set; }
    public SentimentLabel RatingSentiment { get; set; }
    public SentimentLabel? CommentSentiment { get; set; }
    public bool HasRatingCommentConflict { get; set; }
    public bool NeedsManualReview { get; set; }
    public bool HasSafetyRisk { get; set; }
    public int? Severity { get; set; }
    public float? Confidence { get; set; }
    public string? Reason { get; set; }
    public SentimentSource Source { get; set; }
    public DateTime? AnalyzedAtUtc { get; set; }
    public SentimentAlertStatus AlertStatus { get; set; }
    public string? AnalysisVersion { get; set; }
    public List<SentimentAspectDto> Aspects { get; set; } = new();
}
