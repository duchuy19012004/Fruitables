using System.Text.Json;
using Microsoft.Extensions.Logging;
using Fruitables.Constants;
using Fruitables.Models;
using Fruitables.Services.Interfaces;

namespace Fruitables.Services;

/// <summary>
/// Service quản lý phí vận chuyển theo khu vực
/// </summary>
public class ShippingService : IShippingService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ShippingService> _logger;
    private readonly IGhnService _ghnService;

    public ShippingService(
        ISettingsService settingsService,
        ILogger<ShippingService> logger,
        IGhnService ghnService)
    {
        _settingsService = settingsService;
        _logger = logger;
        _ghnService = ghnService;
    }

    /// <inheritdoc/>
    public async Task<ShippingZone> GetShippingZoneAsync(string district)
    {
        // Get config with district lists
        var config = await GetShippingConfigAsync();
        
        // Determine zone based on district
        return DetermineShippingZone(district, config);
    }

    /// <summary>
    /// Determines the shipping zone for a district based on config.
    /// Requirements 3.2: Quận/huyện không thuộc Zone1 hoặc Zone2 sẽ áp dụng Zone3
    /// Requirements 4.1: Tự động xác định khu vực từ địa chỉ
    /// </summary>
    private static ShippingZone DetermineShippingZone(string? district, ShippingConfig config)
    {
        // Null or empty district defaults to Zone3 (fallback behavior)
        if (string.IsNullOrWhiteSpace(district))
        {
            return ShippingZone.Zone3_Remote;
        }
        
        var trimmedDistrict = district.Trim();
        
        // Check Zone1 (Nội thành) - case insensitive comparison
        if (config.Zone1Districts.Any(d => 
            string.Equals(d.Trim(), trimmedDistrict, StringComparison.OrdinalIgnoreCase)))
        {
            return ShippingZone.Zone1_InnerCity;
        }
        
        // Check Zone2 (Ngoại thành) - case insensitive comparison
        if (config.Zone2Districts.Any(d => 
            string.Equals(d.Trim(), trimmedDistrict, StringComparison.OrdinalIgnoreCase)))
        {
            return ShippingZone.Zone2_OuterCity;
        }
        
        // Default to Zone3 (Vùng xa) - Requirements 3.2
        return ShippingZone.Zone3_Remote;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Tính toán phí vận chuyển GHN dựa trên tổng tiền hàng, mã địa chỉ GHN và gói hàng.
    /// Khi có đủ thông tin (subtotal > 0, package hợp lệ, districtId và wardCode không rỗng),
    /// gọi <see cref="IGhnService.CalculateFeeAsync"/> để tính phí và trả về phí GHN.
    /// Nếu thiếu thông tin hoặc GHN trả về null, trả về phí 0 cùng thông báo lỗi GHN.
    /// </summary>
    public async Task<ShippingInfo> CalculateShippingAsync(
        decimal subtotal,
        string district,
        int? ghnDistrictId = null,
        string? ghnWardCode = null,
        ShippingPackage? package = null)
    {
        if (subtotal > 0
            && package != null
            && package.Weight > 0
            && ghnDistrictId.HasValue
            && !string.IsNullOrWhiteSpace(ghnWardCode))
        {
            var ghnFee = await _ghnService.CalculateFeeAsync(
                ghnDistrictId.Value,
                ghnWardCode,
                package.Weight,
                package.Length,
                package.Width,
                package.Height);

            if (ghnFee.HasValue)
            {
                return new ShippingInfo
                {
                    ShippingFee = ghnFee.Value,
                    Zone = ShippingZone.Zone3_Remote,
                    Message = "Phi van chuyen GHN"
                };
            }
        }

        return new ShippingInfo
        {
            ShippingFee = 0m,
            Zone = ShippingZone.Zone3_Remote,
            Message = subtotal > 0
                ? "Không tính được phí vận chuyển GHN"
                : string.Empty
        };
    }

    /// <inheritdoc/>
    public async Task<ShippingConfig> GetShippingConfigAsync()
    {
        var config = new ShippingConfig();

        try
        {
            // Read shipping fees from settings with default values
            var feeZone1 = await _settingsService.GetSettingAsync<decimal?>(
                SettingKeys.ShippingFeeZone1, null);
            var feeZone2 = await _settingsService.GetSettingAsync<decimal?>(
                SettingKeys.ShippingFeeZone2, null);
            var feeZone3 = await _settingsService.GetSettingAsync<decimal?>(
                SettingKeys.ShippingFeeZone3, null);
            var freeShippingThreshold = await _settingsService.GetSettingAsync<decimal?>(
                SettingKeys.FreeShippingThreshold, null);
            var reducedFeeZone3 = await _settingsService.GetSettingAsync<decimal?>(
                SettingKeys.ReducedShippingFeeZone3, null);

            // Validate and apply values (only if valid, otherwise keep defaults)
            if (feeZone1.HasValue && ValidateShippingFee(feeZone1.Value))
                config.FeeZone1 = feeZone1.Value;
            
            if (feeZone2.HasValue && ValidateShippingFee(feeZone2.Value))
                config.FeeZone2 = feeZone2.Value;
            
            if (feeZone3.HasValue && ValidateShippingFee(feeZone3.Value))
                config.FeeZone3 = feeZone3.Value;
            
            if (freeShippingThreshold.HasValue && ValidateShippingFee(freeShippingThreshold.Value))
                config.FreeShippingThreshold = freeShippingThreshold.Value;
            
            if (reducedFeeZone3.HasValue && ValidateShippingFee(reducedFeeZone3.Value))
                config.ReducedFeeZone3 = reducedFeeZone3.Value;

            // Read district lists
            var zone1DistrictsJson = await _settingsService.GetSettingAsync(SettingKeys.Zone1Districts);
            var zone2DistrictsJson = await _settingsService.GetSettingAsync(SettingKeys.Zone2Districts);

            if (!string.IsNullOrEmpty(zone1DistrictsJson))
            {
                try
                {
                    config.Zone1Districts = JsonSerializer.Deserialize<List<string>>(zone1DistrictsJson) 
                        ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Zone1Districts JSON: {Json}", zone1DistrictsJson);
                }
            }

            if (!string.IsNullOrEmpty(zone2DistrictsJson))
            {
                try
                {
                    config.Zone2Districts = JsonSerializer.Deserialize<List<string>>(zone2DistrictsJson) 
                        ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Zone2Districts JSON: {Json}", zone2DistrictsJson);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error and return default config (Requirements 7.1)
            _logger.LogError(ex, "Error reading shipping config from database. Using default values.");
        }

        return config;
    }

    /// <inheritdoc/>
    public bool ValidateShippingFee(decimal fee)
    {
        return fee >= 0;
    }

    /// <inheritdoc/>
    public bool TryParseAndValidateShippingFee(string? value, out decimal fee)
    {
        fee = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!decimal.TryParse(value, out fee))
        {
            return false;
        }

        return ValidateShippingFee(fee);
    }
}
