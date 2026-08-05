using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;

namespace Fruitables.Services.Pricing.Coupons;

public class CouponService : ICouponService
{
    private readonly IUnitOfWork _unitOfWork;

    public CouponService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Coupon>> GetAllAsync()
    {
        return await _unitOfWork.Coupons.Query()
            .OrderByDescending(c => c.Id)
            .ToListAsync();
    }

    public async Task<Coupon?> GetByIdAsync(int id)
    {
        return await _unitOfWork.Coupons.GetByIdAsync(id);
    }

    public async Task<(bool Success, string? Error)> CreateAsync(CreateCouponRequest request)
    {
        var codeUpper = request.Code.ToUpper();
        var exists = await _unitOfWork.Coupons.Query()
            .AnyAsync(c => c.Code == codeUpper);
        if (exists)
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");

        if (request.Type == CouponType.Percentage && request.Value > 100)
            return (false, "Phần trăm giảm giá không được vượt quá 100%");

        var coupon = new Coupon
        {
            Code           = codeUpper,
            Type           = request.Type,
            Value          = request.Value,
            MinOrderAmount = request.MinOrderAmount,
            MinQuantity    = request.MinQuantity,
            MaxUses        = request.MaxUses,
            StartDate      = request.StartDate,
            EndDate        = request.EndDate,
            IsActive       = request.IsActive
        };

        await _unitOfWork.Coupons.AddAsync(coupon);
        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateCouponRequest request)
    {
        var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
        if (coupon == null)
            return (false, "Không tìm thấy mã giảm giá");

        var codeUpper = request.Code.ToUpper();
        var exists = await _unitOfWork.Coupons.Query()
            .AnyAsync(c => c.Code == codeUpper && c.Id != id);
        if (exists)
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");

        if (request.Type == CouponType.Percentage && request.Value > 100)
            return (false, "Phần trăm giảm giá không được vượt quá 100%");

        coupon.Code           = codeUpper;
        coupon.Type           = request.Type;
        coupon.Value          = request.Value;
        coupon.MinOrderAmount = request.MinOrderAmount;
        coupon.MinQuantity    = request.MinQuantity;
        coupon.MaxUses        = request.MaxUses;
        coupon.StartDate      = request.StartDate;
        coupon.EndDate        = request.EndDate;
        coupon.IsActive       = request.IsActive;

        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
        if (coupon == null)
            return (false, "Không tìm thấy mã giảm giá");

        _unitOfWork.Coupons.Remove(coupon);
        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    public async Task<CouponApplyResult> ApplyCouponAsync(string code, decimal subtotal, int itemCount)
    {
        var codeUpper = code.Trim().ToUpper();

        var coupon = await _unitOfWork.Coupons.Query()
            .FirstOrDefaultAsync(c => c.Code == codeUpper);

        if (coupon == null)
            return Fail("Mã giảm giá không tồn tại");

        if (!coupon.IsActive)
            return Fail("Mã giảm giá không còn hiệu lực");

        var now = DateTime.UtcNow.AddHours(7);
        if (coupon.StartDate.HasValue && now < coupon.StartDate.Value)
            return Fail("Mã giảm giá chưa đến thời gian sử dụng");

        if (coupon.EndDate.HasValue && now > coupon.EndDate.Value)
            return Fail("Mã giảm giá đã hết hạn");

        if (itemCount < coupon.MinQuantity)
            return Fail($"Mã này yêu cầu mua tối thiểu {coupon.MinQuantity} sản phẩm (bạn có {itemCount})");

        if (subtotal < coupon.MinOrderAmount)
            return Fail($"Đơn hàng phải từ {coupon.MinOrderAmount:N0}đ để dùng mã này (hiện tại {subtotal:N0}đ)");

        if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value)
            return Fail("Mã giảm giá đã hết lượt sử dụng");

        decimal discount = coupon.Type == CouponType.Percentage
            ? Math.Round(subtotal * coupon.Value / 100, 0)
            : coupon.Value;

        discount = Math.Min(discount, subtotal);

        string desc = coupon.Type == CouponType.Percentage
            ? $"Giảm {coupon.Value}%"
            : $"Giảm {coupon.Value:N0}đ";

        return new CouponApplyResult
        {
            Success        = true,
            DiscountAmount = discount,
            CouponCode     = coupon.Code,
            Message        = $"Áp dụng thành công! {desc}"
        };
    }

    private static CouponApplyResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    public async Task<List<CouponEligibilityResult>> GetAvailableCouponsAsync(decimal subtotal, int itemCount)
    {
        var now = DateTime.UtcNow.AddHours(7);

        var coupons = await _unitOfWork.Coupons.Query()
            .Where(c => c.IsActive
                && (c.StartDate == null || c.StartDate <= now)
                && (c.EndDate == null || c.EndDate >= now)
                && (c.MaxUses == null || c.UsedCount < c.MaxUses))
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        var result = new List<CouponEligibilityResult>();

        foreach (var c in coupons)
        {
            string? reason = null;

            if (itemCount < c.MinQuantity)
                reason = $"Cần mua thêm {c.MinQuantity - itemCount} sản phẩm";
            else if (subtotal < c.MinOrderAmount)
                reason = $"Cần thêm {(c.MinOrderAmount - subtotal):N0}đ để đủ điều kiện";

            decimal discount = 0;
            if (reason == null)
            {
                discount = c.Type == CouponType.Percentage
                    ? Math.Round(subtotal * c.Value / 100, 0)
                    : c.Value;
                discount = Math.Min(discount, subtotal);
            }

            result.Add(new CouponEligibilityResult
            {
                Id               = c.Id,
                Code             = c.Code,
                Type             = c.Type,
                Value            = c.Value,
                MinOrderAmount   = c.MinOrderAmount,
                MinQuantity      = c.MinQuantity,
                EndDate          = c.EndDate,
                IsEligible       = reason == null,
                IneligibleReason = reason,
                DiscountAmount   = discount
            });
        }

        return result;
    }
}
