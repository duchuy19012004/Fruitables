using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Repositories.Interfaces;

/// <summary>
/// Repository interface cho ReviewReport entity
/// </summary>
public interface IReviewReportRepository
{
    /// <summary>
    /// Lấy reports của review
    /// </summary>
    Task<List<ReviewReport>> GetReportsByReviewIdAsync(int reviewId);
    
    /// <summary>
    /// Lấy pending reports với pagination
    /// </summary>
    Task<PagedResult<ReviewReport>> GetPendingReportsAsync(ReportFilterDto filter);
    
    /// <summary>
    /// Lấy tất cả reports với pagination và filtering
    /// </summary>
    Task<PagedResult<ReviewReport>> GetAllReportsAsync(ReportFilterDto filter);
    
    /// <summary>
    /// Kiểm tra user đã report review chưa
    /// </summary>
    Task<bool> HasUserReportedReviewAsync(int userId, int reviewId);
    
    /// <summary>
    /// Đếm số reports pending
    /// </summary>
    Task<int> CountPendingReportsAsync();
}
