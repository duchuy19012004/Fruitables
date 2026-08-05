using Fruitables.ViewModels;

namespace Fruitables.Services.Reviews;

/// <summary>
/// Service interface cho Review operations
/// </summary>
public interface IReviewService
{
    // Customer operations
    Task<ReviewResult> CreateReviewAsync(CreateReviewDto dto, int userId);
    Task<ReviewResult> UpdateReviewAsync(int reviewId, UpdateReviewDto dto, int userId);
    Task<bool> DeleteReviewAsync(int reviewId, int userId);
    Task<PagedResult<ReviewViewModel>> GetProductReviewsAsync(ReviewFilterDto filter, int? currentUserId = null);
    Task<ReviewStatistics> GetProductReviewStatisticsAsync(int productId);
    Task<bool> CanUserReviewProductAsync(int userId, int productId);
    Task<ReviewPermission> GetReviewPermissionAsync(int userId, int productId);
    Task<bool> ReportReviewAsync(int reviewId, ReportReviewDto dto, int userId);
    Task<bool> MarkReviewHelpfulAsync(int reviewId, int userId);
    
    // Admin operations
    Task<PagedResult<ReviewAdminViewModel>> GetAllReviewsAsync(ReviewAdminFilterDto filter);
    Task<bool> HideReviewAsync(int reviewId, string reason, int adminId);
    Task<bool> ShowReviewAsync(int reviewId, int adminId);
    Task<bool> DeleteReviewByAdminAsync(int reviewId, string reason, int adminId);
    Task<PagedResult<ReviewReportViewModel>> GetReviewReportsAsync(ReportFilterDto filter);
    Task<bool> HandleReportAsync(int reportId, ReportAction action, int adminId);
    Task<ReviewAdminStatistics> GetAdminStatisticsAsync();
    
    // Internal operations
    Task RecalculateProductRatingAsync(int productId);
    Task<bool> CheckVerifiedPurchaseAsync(int userId, int productId);
    Task<object> GetUserOrdersWithProductAsync(int userId, int productId);
}
