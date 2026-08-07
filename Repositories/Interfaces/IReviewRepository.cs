using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Repositories.Interfaces;

// Interface repository cho Review entity: thêm các truy vấn đặc thù ngoài CRUD cơ bản.
public interface IReviewRepository
{
    // Lấy danh sách review của 1 sản phẩm với phân trang, sắp xếp, lọc
    Task<PagedResult<Review>> GetProductReviewsAsync(ReviewFilterDto filter);
    
    // Lấy 1 review kèm thông tin user + product (includes)
    Task<Review?> GetReviewWithDetailsAsync(int reviewId);
    
    // Lấy review của 1 user cho 1 sản phẩm cụ thể (kiểm tra đã review chưa)
    Task<Review?> GetUserReviewForProductAsync(int userId, int productId);
    
    // Thống kê đánh giá sản phẩm: điểm TB, phân bố sao, tổng số review
    Task<ReviewStatistics> GetProductReviewStatisticsAsync(int productId);
    
    // Lấy tất cả reviews cho admin (phân trang + filter theo trạng thái, ngày, search)
    Task<PagedResult<Review>> GetAllReviewsForAdminAsync(ReviewAdminFilterDto filter);
    
    // Kiểm tra user đã review sản phẩm chưa (true/false)
    Task<bool> HasUserReviewedProductAsync(int userId, int productId);
    
    // Đếm số reviews của user trong khoảng thời gian (giới hạn spam)
    Task<int> CountUserReviewsInPeriodAsync(int userId, DateTime fromDate);
    
    // Lấy danh sách review chờ kiểm duyệt (pending moderation)
    Task<List<Review>> GetPendingReviewsAsync();
    
    // Lấy review bị báo cáo nhiều lần (theo ngưỡng minReportCount)
    Task<List<Review>> GetReportedReviewsAsync(int minReportCount = 1);
}
