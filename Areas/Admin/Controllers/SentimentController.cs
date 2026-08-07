using System.Globalization;
using System.Security.Claims;
using System.Text;
using Fruitables.Attributes;
using Fruitables.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Services.Reviews;
using Fruitables.Services.Sentiment;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[RequirePermission("reviews.analyze")]
public class SentimentController : Controller
{
    private readonly ISentimentAnalysisService _sentimentService;
    private readonly ITestimonialService _testimonialService;
    private readonly IEmailService _emailService;
    private readonly ILogger<SentimentController> _logger;

    public SentimentController(ISentimentAnalysisService sentimentService, ITestimonialService testimonialService, IEmailService emailService, ILogger<SentimentController> logger)
    {
        _sentimentService = sentimentService;
        _testimonialService = testimonialService;
        _emailService = emailService;
        _logger = logger;
    }

    // Dashboard phân tích cảm xúc
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        var rangeTo = (to ?? today).Date;
        rangeTo = rangeTo > today ? today : rangeTo;
        var rangeFrom = (from ?? rangeTo.AddDays(-13)).Date;
        if (rangeFrom > rangeTo) rangeFrom = rangeTo;
        ViewBag.DashboardFrom = rangeFrom;
        ViewBag.DashboardTo = rangeTo;

        try
        {
            var data = await _sentimentService.GetDashboardAsync(rangeFrom, rangeTo);
            var alerts = await _sentimentService.GetReviewsAsync(new SentimentReviewFilter { AlertOnly = true, PageSize = 20 });
            ViewBag.PendingAlerts = alerts.Items;
            return View(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sentiment dashboard");
            TempData["Error"] = "Có lỗi xảy ra khi tải bảng phân tích cảm xúc";
            return View(new SentimentDashboardData());
        }
    }

    // Danh sách review theo cảm xúc
    [HttpGet]
    public async Task<IActionResult> Reviews(SentimentReviewFilter filter)
    {
        try
        {
            var result = await _sentimentService.GetReviewsAsync(filter);
            return View(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sentiment reviews");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách đánh giá";
            return View(new PagedSentimentReviews());
        }
    }

    // Admin sửa tay nhãn cảm xúc
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.analyze_override")]
    public async Task<IActionResult> Override(int reviewId, SentimentLabel label, int? severity, string? note)
    {
        try
        {
            var success = await _sentimentService.OverrideAsync(reviewId, label, severity, note, GetCurrentUserId());
            if (!success) return BadRequest(new { success = false, message = "Không tìm thấy phân tích hoặc conflict cần ghi chú khi duyệt" });
            return Ok(new { success = true, message = "Đã cập nhật nhãn cảm xúc" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error overriding sentiment for review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // Xác nhận cảnh báo tiêu cực nghiêm trọng
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.analyze_override")]
    public async Task<IActionResult> Acknowledge(int reviewId)
    {
        try
        {
            var success = await _sentimentService.AcknowledgeAlertAsync(reviewId, GetCurrentUserId());
            if (!success) return BadRequest(new { success = false, message = "Cảnh báo không tồn tại hoặc đã xử lý" });
            return Ok(new { success = true, message = "Đã xác nhận cảnh báo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert for review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // Chạy backfill review chưa phân tích
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.analyze_trigger")]
    public async Task<IActionResult> Backfill()
    {
        try
        {
            var chunks = await _sentimentService.EnqueueBackfillAsync();
            return Ok(new { success = true, message = $"Đã xếp hàng phân tích {chunks} đợt review", chunks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering sentiment backfill");
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi chạy backfill" });
        }
    }

    // Export CSV báo cáo theo bộ lọc
    [HttpGet]
    public async Task<IActionResult> ExportCsv(SentimentReviewFilter filter)
    {
        try
        {
            filter.Page = 1;
            filter.PageSize = 5000;
            var result = await _sentimentService.GetReviewsAsync(filter, maxPageSize: 5000);

            var sb = new StringBuilder();
            sb.AppendLine("ReviewId,ProductId,Sản phẩm,Khách hàng,Rating,RatingSentiment,CommentSentiment,Conflict,NeedsManualReview,SafetyRisk,Ngày,Cảm xúc,Severity,Độ tin cậy,Lý do,Nguồn,Khía cạnh,Verified,Comment");
            foreach (var row in result.Items)
            {
                var aspects = string.Join("; ", row.Aspects.Select(a => $"{a.Aspect}:{a.Sentiment}"));
                sb.Append(string.Join(",",
                    row.ReviewId,
                    row.ProductId,
                    Csv(row.ProductName),
                    Csv(row.UserName),
                    row.Rating,
                    row.RatingSentiment,
                    row.CommentSentiment?.ToString() ?? "",
                    row.HasRatingCommentConflict,
                    row.NeedsManualReview,
                    row.HasSafetyRisk,
                    row.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    row.Label,
                    row.Severity?.ToString() ?? "",
                    row.Confidence?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                    Csv(row.Reason ?? ""),
                    row.Source,
                    Csv(aspects),
                    row.IsVerifiedPurchase,
                    Csv(row.Comment)));
                sb.AppendLine();
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"sentiment-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sentiment CSV");
            return BadRequest(new { success = false, message = "Có lỗi xảy ra khi export" });
        }
    }

    // Chuyển tới đơn hàng của khách chứa sản phẩm bị review (CSKH chủ động)
    [HttpGet]
    public async Task<IActionResult> OrderInfo(int reviewId)
    {
        try
        {
            var context = await _sentimentService.GetReviewContextAsync(reviewId);
            if (context?.OrderId is null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng nào chứa sản phẩm này" });

            return Json(new { success = true, orderId = context.OrderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding order for review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // Gửi email hỏi thăm khách sau review tiêu cực
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.analyze_override")]
    public async Task<IActionResult> SendFollowUp(int reviewId, string subject, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return BadRequest(new { success = false, message = "Vui lòng nhập nội dung email" });

            var context = await _sentimentService.GetReviewContextAsync(reviewId);
            if (context is null)
                return BadRequest(new { success = false, message = "Không tìm thấy đánh giá" });

            var ok = await _emailService.SendFollowUpEmailAsync(
                context.UserEmail,
                context.UserName,
                string.IsNullOrWhiteSpace(subject) ? $"[{context.ProductName}] Cảm ơn phản hồi của bạn" : subject,
                message);

            if (!ok)
                return StatusCode(500, new { success = false, message = "Không gửi được email (kiểm tra cấu hình SMTP)" });

            return Ok(new { success = true, message = $"Đã gửi email tới {context.UserEmail}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending follow-up email for review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // LLM gợi ý phản hồi cho review tiêu cực (admin duyệt/sửa)
    [HttpGet]
    public async Task<IActionResult> GenerateReply(int reviewId)
    {
        try
        {
            var draft = await _sentimentService.GenerateReplyDraftAsync(reviewId);
            if (draft is null)
                return StatusCode(502, new { success = false, message = "Không tạo được gợi ý (kiểm tra API LLM)" });
            return Ok(new { success = true, draft });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reply draft for review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // Đề xuất testimonial từ review tích cực (chờ admin duyệt trên trang Testimonial)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestTestimonial(int reviewId)
    {
        try
        {
            var testimonial = await _testimonialService.SuggestFromReviewAsync(reviewId);
            if (testimonial is null)
                return BadRequest(new { success = false, message = "Chỉ đề xuất được review 4-5 sao, tích cực và có nội dung" });
            return Ok(new { success = true, message = $"Đã thêm đề xuất testimonial #{testimonial.Id} — chờ duyệt" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting testimonial from review {ReviewId}", reviewId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    private static string Csv(string value) => CsvEscaper.Escape(value);

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }
}
