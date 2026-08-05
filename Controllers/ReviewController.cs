using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Reviews;

namespace Fruitables.Controllers;

// Controller đánh giá sản phẩm: CRUD + report + mark helpful + debug.
// Yêu cầu đăng nhập ([Authorize]), ngoại trừ GetReviews (AllowAnonymous).
[Authorize]
[Route("[controller]")]
public class ReviewController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    // POST: /Review/Create — tạo đánh giá mới (JSON body)
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
    {
        try
        {
            // Validate input
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            var userId = GetCurrentUserId();
            var result = await _reviewService.CreateReviewAsync(dto, userId);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage, errorCode = result.ErrorCode });
            }

            return Ok(new { success = true, message = "Đánh giá của bạn đã được gửi thành công", data = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tạo đánh giá" });
        }
    }

    // PUT: /Review/{id} — cập nhật đánh giá
    [HttpPut("{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] UpdateReviewDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            var userId = GetCurrentUserId();
            var result = await _reviewService.UpdateReviewAsync(id, dto, userId);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage, errorCode = result.ErrorCode });
            }

            return Ok(new { success = true, message = "Đánh giá đã được cập nhật", data = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi cập nhật đánh giá" });
        }
    }

    // DELETE: /Review/{id} — xóa đánh giá
    [HttpDelete("{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _reviewService.DeleteReviewAsync(id, userId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể xóa đánh giá" });
            }

            return Ok(new { success = true, message = "Đánh giá đã được xóa" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xóa đánh giá" });
        }
    }

    // POST: /Review/{id}/report — báo cáo vi phạm
    [HttpPost("{id}/report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int id, [FromBody] ReportReviewDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            var userId = GetCurrentUserId();
            var success = await _reviewService.ReportReviewAsync(id, dto, userId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể báo cáo đánh giá này" });
            }

            return Ok(new { success = true, message = "Báo cáo của bạn đã được gửi" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi báo cáo" });
        }
    }

    // POST: /Review/{id}/helpful — đánh dấu hữu ích
    [HttpPost("{id}/helpful")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkHelpful(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _reviewService.MarkReviewHelpfulAsync(id, userId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể đánh dấu hữu ích" });
            }

            return Ok(new { success = true, message = "Cảm ơn phản hồi của bạn" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking review {ReviewId} as helpful", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // GET: /Review/can-review/{productId} — kiểm tra user có được review sản phẩm không
    [HttpGet("can-review/{productId}")]
    public async Task<IActionResult> CanReview(int productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var canReview = await _reviewService.CanUserReviewProductAsync(userId, productId);

            return Ok(new { canReview });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user can review product {ProductId}", productId);
            return StatusCode(500, new { canReview = false });
        }
    }

    // GET: /Review/check-permission/{productId} — debug chi tiết quyền review
    [HttpGet("check-permission/{productId}")]
    public async Task<IActionResult> CheckPermission(int productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var permission = await _reviewService.GetReviewPermissionAsync(userId, productId);
            var hasPurchased = await _reviewService.CheckVerifiedPurchaseAsync(userId, productId);

            return Ok(new
            {
                userId,
                productId,
                permission = permission.ToString(),
                canReview = permission == ReviewPermission.Allowed,
                hasPurchased
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for product {ProductId}", productId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: /Review/debug-orders/{productId} — debug xem user có đơn mua sản phẩm không
    [HttpGet("debug-orders/{productId}")]
    public async Task<IActionResult> DebugOrders(int productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var orders = await _reviewService.GetUserOrdersWithProductAsync(userId, productId);

            return Ok(new
            {
                userId,
                productId,
                orders
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error debugging orders for user {UserId} and product {ProductId}", GetCurrentUserId(), productId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: /Review/GetReviews — lấy danh sách review qua AJAX (không cần login)
    [HttpGet("GetReviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(int productId, string sortBy = "newest", int page = 1)
    {
        try
        {
            // Parse sortBy string sang enum, mặc định Newest
            ReviewSortBy sortByEnum = ReviewSortBy.Newest;
            if (!string.IsNullOrEmpty(sortBy) && Enum.TryParse<ReviewSortBy>(sortBy, true, out var parsedSort))
            {
                sortByEnum = parsedSort;
            }

            var userId = GetCurrentUserIdOrZero();
            var filter = new ReviewFilterDto
            {
                ProductId = productId,
                Page = page,
                PageSize = 10,
                SortBy = sortByEnum
            };

            var reviews = await _reviewService.GetProductReviewsAsync(filter, userId);

            return Json(new
            {
                success = true,
                totalCount = reviews.TotalCount,
                totalPages = reviews.TotalPages,
                currentPage = reviews.CurrentPage,
                items = reviews.Items.Select(r => new
                {
                    r.Id,
                    r.UserName,
                    r.UserAvatar,
                    r.Rating,
                    r.Comment,
                    r.IsOwner,
                    r.IsVerifiedPurchase,
                    r.HelpfulCount,
                    createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = r.UpdatedAt.HasValue && r.UpdatedAt.Value != r.CreatedAt
                        ? r.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm")
                        : (string?)null
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for product {ProductId}", productId);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải đánh giá" });
        }
    }

    // Helper: lấy userId, throw nếu chưa đăng nhập
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // Helper: lấy userId, trả 0 nếu anonymous (dùng cho GetReviews)
    private int GetCurrentUserIdOrZero()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId)) return userId;
        }
        return 0;
    }
}
