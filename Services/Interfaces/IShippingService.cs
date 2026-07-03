using Fruitables.Models;

namespace Fruitables.Services.Interfaces;

/// <summary>
/// Interface cho Shipping Service - quản lý phí vận chuyển theo khu vực
/// </summary>
public interface IShippingService
{
    /// <summary>
    /// Xác định khu vực giao hàng từ quận/huyện
    /// </summary>
    /// <param name="district">Tên quận/huyện</param>
    /// <returns>Khu vực giao hàng (Zone1, Zone2, hoặc Zone3)</returns>
    Task<ShippingZone> GetShippingZoneAsync(string district);

    /// <summary>
    /// Tính toán phí vận chuyển dựa trên tổng tiền hàng và quận/huyện
    /// </summary>
    /// <param name="subtotal">Tổng tiền hàng</param>
    /// <param name="district">Tên quận/huyện giao hàng</param>
    /// <param name="ghnDistrictId">Mã quận/huyện GHN</param>
    /// <param name="ghnWardCode">Mã phường/xã GHN</param>
    /// <param name="packageSize">Kích thước gói hàng dùng cho GHN</param>
    /// <returns>Thông tin phí vận chuyển đầy đủ</returns>
    Task<ShippingInfo> CalculateShippingAsync(
        decimal subtotal,
        string district,
        int? ghnDistrictId = null,
        string? ghnWardCode = null,
        PackageSize? packageSize = null);

    /// <summary>
    /// Lấy cấu hình phí vận chuyển hiện tại
    /// </summary>
    /// <returns>Cấu hình phí vận chuyển (với giá trị mặc định nếu không có trong DB)</returns>
    Task<ShippingConfig> GetShippingConfigAsync();

    /// <summary>
    /// Validate giá trị phí vận chuyển
    /// </summary>
    /// <param name="fee">Giá trị phí cần validate</param>
    /// <returns>True nếu hợp lệ (>= 0), False nếu không hợp lệ</returns>
    bool ValidateShippingFee(decimal fee);

    /// <summary>
    /// Validate và parse chuỗi thành phí vận chuyển
    /// </summary>
    /// <param name="value">Chuỗi cần parse</param>
    /// <param name="fee">Giá trị phí sau khi parse</param>
    /// <returns>True nếu parse và validate thành công, False nếu thất bại</returns>
    bool TryParseAndValidateShippingFee(string? value, out decimal fee);
}
