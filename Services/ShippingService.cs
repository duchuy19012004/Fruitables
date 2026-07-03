using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly GhnOptions _ghnOptions;

    public ShippingService(
        ISettingsService settingsService,
        ILogger<ShippingService> logger,
        IGhnService ghnService,
        IOptions<GhnOptions> ghnOptions)
    {
        _settingsService = settingsService;
        _logger = logger;
        _ghnService = ghnService;
        _ghnOptions = ghnOptions.Value;
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
    /// Tính toán phí vận chuyển dựa trên tổng tiền hàng và quận/huyện.
    /// Requirements 4.2, 4.3, 4.4: Tính phí theo zone và ngưỡng miễn phí
    /// Requirements 5.1, 5.2, 5.3, 5.4: Hiển thị message phù hợp
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
                ? "Khong tinh duoc phi van chuyen GHN"
                : string.Empty
        };
    }

    /// <summary>
    /// Internal method to calculate shipping based on subtotal, zone, and config.
    /// Implements Property 4, 5, and 6 from design document.
    /// </summary>
    private static ShippingInfo CalculateShippingInternal(decimal subtotal, ShippingZone zone, ShippingConfig config)
    {
        var result = new ShippingInfo
        {
            Zone = zone,
            IsFreeShipping = false,
            IsReducedShipping = false,
            AmountToFreeShipping = 0m,
            Message = string.Empty
        };

        // Rule 1: If subtotal = 0, shipping = 0 (Requirements 4.4)
        if (subtotal == 0)
        {
            result.ShippingFee = 0m;
            return result;
        }

        // Rule 2: If threshold > 0 and subtotal >= threshold (Requirements 4.2, 4.3)
        if (config.FreeShippingThreshold > 0 && subtotal >= config.FreeShippingThreshold)
        {
            if (zone == ShippingZone.Zone1_InnerCity || zone == ShippingZone.Zone2_OuterCity)
            {
                // Zone1 or Zone2: free shipping (Requirements 4.2)
                result.ShippingFee = 0m;
                result.IsFreeShipping = true;
                result.Message = "Miễn phí vận chuyển"; // Requirements 5.2
            }
            else
            {
                // Zone3: reduced shipping (Requirements 4.2)
                result.ShippingFee = config.ReducedFeeZone3;
                result.IsReducedShipping = true;
                result.Message = $"Phí vận chuyển giảm còn {config.ReducedFeeZone3:N0} đ (đơn hàng đạt ngưỡng)"; // Requirements 5.3
            }
        }
        else
        {
            // Rule 3: Otherwise, shipping = feeByZone (Requirements 4.3)
            result.ShippingFee = GetFeeForZone(config, zone);
            
            // Calculate amount to free shipping and set message (Requirements 5.1)
            if (config.FreeShippingThreshold > 0)
            {
                result.AmountToFreeShipping = config.FreeShippingThreshold - subtotal;
                result.Message = $"Mua thêm {result.AmountToFreeShipping:N0} đ để được miễn phí vận chuyển";
            }
            // If threshold = 0, no message about free shipping (Requirements 5.4)
        }

        return result;
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

    /// <summary>
    /// Gets the shipping fee for a specific zone from config
    /// </summary>
    private static decimal GetFeeForZone(ShippingConfig config, ShippingZone zone)
    {
        return zone switch
        {
            ShippingZone.Zone1_InnerCity => config.FeeZone1,
            ShippingZone.Zone2_OuterCity => config.FeeZone2,
            ShippingZone.Zone3_Remote => config.FeeZone3,
            _ => config.FeeZone3 // Default to Zone3 fee
        };
    }
}
