using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Fruitables.Services;

public class GhnService : IGhnService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly GhnOptions _options;
    private readonly ILogger<GhnService> _logger;

    public GhnService(HttpClient httpClient, IOptions<GhnOptions> options, ILogger<GhnService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    public async Task<decimal?> CalculateFeeAsync(
        int toDistrictId,
        string toWardCode,
        int weight,
        int length,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured || toDistrictId <= 0 || string.IsNullOrWhiteSpace(toWardCode))
            return null;

        var request = new HttpRequestMessage(HttpMethod.Post, "v2/shipping-order/fee")
        {
            Content = JsonContent.Create(new
            {
                service_type_id = _options.ServiceTypeId,
                from_district_id = _options.FromDistrictId,
                from_ward_code = _options.FromWardCode,
                to_district_id = toDistrictId,
                to_ward_code = toWardCode,
                weight,
                length,
                width,
                height
            })
        };

        request.Headers.Add("Token", _options.Token);
        request.Headers.Add("ShopId", _options.ShopId.ToString(CultureInfo.InvariantCulture));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GHN fee request failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GhnFeeResponse>(JsonOptions, cancellationToken);
            return payload?.Code == 200 ? payload.Data?.Total : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "GHN fee request failed");
            return null;
        }
    }

    public async Task<GhnAddressCode?> ResolveAddressAsync(
        string provinceName,
        string wardOrCommuneName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured || string.IsNullOrWhiteSpace(provinceName) || string.IsNullOrWhiteSpace(wardOrCommuneName))
            return null;

        var provinces = await GetAsync<GhnListResponse<GhnProvince>>("master-data/province", cancellationToken);
        if (provinces?.Code != 200)
            return null;

        var province = provinces?.Data.FirstOrDefault(p => SameName(p.ProvinceName, provinceName));
        if (province == null)
            return null;

        var districts = await PostAsync<GhnListResponse<GhnDistrict>>("master-data/district", new { province_id = province.ProvinceId }, cancellationToken);
        if (districts?.Code != 200)
            return null;

        foreach (var district in districts?.Data ?? Enumerable.Empty<GhnDistrict>())
        {
            var wards = await PostAsync<GhnListResponse<GhnWard>>($"master-data/ward?district_id={district.DistrictId}", new { district_id = district.DistrictId }, cancellationToken);
            if (wards?.Code != 200)
                continue;

            var ward = wards?.Data.FirstOrDefault(w => SameName(w.WardName, wardOrCommuneName));
            if (ward != null)
                return new GhnAddressCode(district.DistrictId, ward.WardCode);
        }

        return null;
    }

    private async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Token", _options.Token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                : default;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "GHN GET {Uri} failed", uri);
            return default;
        }
    }

    private async Task<T?> PostAsync<T>(string uri, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Token", _options.Token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                : default;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "GHN POST {Uri} failed", uri);
            return default;
        }
    }

    private static bool SameName(string? left, string? right) =>
        Normalize(left) == Normalize(right);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("\u0111", "d")
            .Replace("phuong ", "")
            .Replace("xa ", "")
            .Replace("thi tran ", "")
            .Trim();
    }

    private sealed class GhnFeeResponse
    {
        public int Code { get; set; }
        public GhnFeeData? Data { get; set; }
    }

    private sealed class GhnFeeData
    {
        public decimal Total { get; set; }
    }

    private sealed class GhnListResponse<T>
    {
        public int Code { get; set; }
        public List<T> Data { get; set; } = new();
    }

    private sealed class GhnProvince
    {
        [JsonPropertyName("ProvinceID")]
        public int ProvinceId { get; set; }

        [JsonPropertyName("ProvinceName")]
        public string ProvinceName { get; set; } = string.Empty;
    }

    private sealed class GhnDistrict
    {
        [JsonPropertyName("DistrictID")]
        public int DistrictId { get; set; }

        [JsonPropertyName("DistrictName")]
        public string DistrictName { get; set; } = string.Empty;
    }

    private sealed class GhnWard
    {
        [JsonPropertyName("WardCode")]
        public string WardCode { get; set; } = string.Empty;

        [JsonPropertyName("WardName")]
        public string WardName { get; set; } = string.Empty;
    }
}
