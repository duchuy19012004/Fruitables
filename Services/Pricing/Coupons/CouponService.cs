using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.ViewModels;

namespace Fruitables.Services.Pricing.Coupons;

public class CouponService : ICouponService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter? _auditLogWriter;

    public CouponService(
        IUnitOfWork unitOfWork,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? (dbContext == null ? null : new AuditLogWriter(dbContext));
    }

    public async Task<List<Coupon>> GetAllAsync()
    {
        return await LoadCouponsAsync();
    }

    public async Task<Coupon?> GetByIdAsync(int id)
    {
        if (_dbContext == null)
            return null;

        var promotion = await _dbContext.Promotions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.Type == "coupon");
        return promotion == null ? null : ToCoupon(promotion);
    }

    public async Task<(bool Success, string? Error)> CreateAsync(CreateCouponRequest request)
    {
        if (_dbContext == null)
            return (false, "Dịch vụ coupon chưa được cấu hình.");

        var codeUpper = request.Code.ToUpperInvariant();
        var exists = (await LoadCouponsAsync()).Any(coupon => coupon.Code == codeUpper);
        if (exists)
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");

        if (request.Type == CouponType.Percentage && request.Value > 100)
            return (false, "Phần trăm giảm giá không được vượt quá 100%");
        if (request.MinQuantity <= 0)
            return (false, "Số lượng tối thiểu phải lớn hơn 0");

        var payload = new CouponPayload
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

        var promotion = new Promotion
        {
            Type = "coupon",
            Code = $"coupon:new-{Guid.NewGuid():N}",
            CustomerCode = payload.Code,
            PayloadJson = _serializer.Serialize(payload),
            IsActive = payload.IsActive,
            StartsAt = ToOffset(payload.StartDate),
            EndsAt = ToOffset(payload.EndDate),
            Revision = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        try
        {
            _dbContext.Promotions.Add(promotion);
            await _dbContext.SaveChangesAsync();
            promotion.Code = $"coupon:{promotion.Id}";
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");
        }
        await WriteAuditAsync("Create", promotion.Id, 0, payload);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateCouponRequest request)
    {
        if (_dbContext == null)
            return (false, "Dịch vụ coupon chưa được cấu hình.");

        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(item => item.Id == id && item.Type == "coupon");
        if (promotion == null)
            return (false, "Không tìm thấy mã giảm giá");

        var current = ToCouponPayload(promotion);

        var codeUpper = request.Code.ToUpperInvariant();
        var exists = (await LoadCouponsAsync()).Any(coupon => coupon.Id != id && coupon.Code == codeUpper);
        if (exists)
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");

        if (request.Type == CouponType.Percentage && request.Value > 100)
            return (false, "Phần trăm giảm giá không được vượt quá 100%");
        if (request.MinQuantity <= 0)
            return (false, "Số lượng tối thiểu phải lớn hơn 0");

        var payload = new CouponPayload
        {
            Code = codeUpper,
            Type = request.Type,
            Value = request.Value,
            MinOrderAmount = request.MinOrderAmount,
            MinQuantity = request.MinQuantity,
            MaxUses = request.MaxUses,
            UsedCount = current.UsedCount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };
        promotion.PayloadJson = _serializer.Serialize(payload);
        promotion.CustomerCode = payload.Code;
        promotion.IsActive = payload.IsActive;
        promotion.StartsAt = ToOffset(payload.StartDate);
        promotion.EndsAt = ToOffset(payload.EndDate);
        promotion.Revision++;
        promotion.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Mã giảm giá đã được người khác cập nhật. Vui lòng tải lại trang");
        }
        catch (DbUpdateException)
        {
            return (false, $"Mã giảm giá '{codeUpper}' đã tồn tại");
        }
        await WriteAuditAsync("Update", promotion.Id, 0, payload);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        if (_dbContext == null)
            return (false, "Dịch vụ coupon chưa được cấu hình.");

        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(item => item.Id == id && item.Type == "coupon");
        if (promotion == null)
            return (false, "Không tìm thấy mã giảm giá");

        _dbContext.Promotions.Remove(promotion);
        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync("Delete", id, 0, null);
        return (true, null);
    }

    public async Task<CouponApplyResult> ApplyCouponAsync(string code, decimal subtotal, decimal itemCount)
    {
        var codeUpper = code.Trim().ToUpperInvariant();

        var coupon = (await LoadCouponsAsync()).FirstOrDefault(c => c.Code == codeUpper);

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

    public async Task<List<CouponEligibilityResult>> GetAvailableCouponsAsync(decimal subtotal, decimal itemCount)
    {
        var now = DateTime.UtcNow.AddHours(7);

        var coupons = (await LoadCouponsAsync())
            .Where(c => c.IsActive
                && (c.StartDate == null || c.StartDate <= now)
                && (c.EndDate == null || c.EndDate >= now)
                && (c.MaxUses == null || c.UsedCount < c.MaxUses))
            .OrderByDescending(c => c.Id)
            .ToList();

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

    private async Task<List<Coupon>> LoadCouponsAsync()
    {
        if (_dbContext == null)
            return [];

        var promotions = await _dbContext.Promotions.AsNoTracking()
            .Where(item => item.Type == "coupon")
            .OrderByDescending(item => item.Id)
            .ToListAsync();
        return promotions.Select(ToCoupon).ToList();
    }

    private Coupon ToCoupon(Promotion promotion)
    {
        var payload = ToCouponPayload(promotion);
        return new Coupon
        {
            Id = promotion.Id,
            Code = payload.Code,
            Type = payload.Type,
            Value = payload.Value,
            MinOrderAmount = payload.MinOrderAmount,
            MinQuantity = payload.MinQuantity,
            MaxUses = payload.MaxUses,
            UsedCount = payload.UsedCount,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            IsActive = payload.IsActive,
            Revision = promotion.Revision
        };
    }

    private CouponPayload ToCouponPayload(Promotion promotion) =>
        _serializer.Deserialize<CouponPayload>(promotion.PayloadJson);

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(value.Value) : null;

    private Task WriteAuditAsync(string action, int entityId, int adminId, CouponPayload? payload) =>
        _auditLogWriter == null
            ? Task.CompletedTask
            : _auditLogWriter.WriteAsync(
                action,
                "Coupon",
                entityId,
                adminId,
                newValue: payload == null ? null : _serializer.Serialize(payload));
}
