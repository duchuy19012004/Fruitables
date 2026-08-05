using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Pricing.Coupons;

public interface ICouponService
{
    Task<List<Coupon>> GetAllAsync();
    Task<Coupon?> GetByIdAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(CreateCouponRequest request);
    Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateCouponRequest request);
    Task<(bool Success, string? Error)> DeleteAsync(int id);

    // Kiểm tra mã và tính discount. Trả về kết quả áp dụng
    Task<CouponApplyResult> ApplyCouponAsync(string code, decimal subtotal, int itemCount);

    // Lấy danh sách coupon đang hoạt động với trạng thái đủ điều kiện
    Task<List<CouponEligibilityResult>> GetAvailableCouponsAsync(decimal subtotal, int itemCount);
}
