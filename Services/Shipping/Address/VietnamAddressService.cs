using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fruitables.Services.Shipping.Address;

public class VietnamAddressService : IVietnamAddressService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public VietnamAddressService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProvinceDto>> GetProvincesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("latest/provinces");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<AddressKitProvincesResponse>(json, JsonOptions);
            var provinces = wrapper?.Provinces;

            if (provinces == null || provinces.Count == 0)
                return VietnamAddressData.GetProvinces();

            return provinces
                .OrderBy(p => p.Name, StringComparer.Create(new CultureInfo("vi-VN"), false))
                .ToList();
        }
        catch (TaskCanceledException)
        {
            return VietnamAddressData.GetProvinces();
        }
        catch (HttpRequestException)
        {
            return VietnamAddressData.GetProvinces();
        }
        catch (Exception)
        {
            return VietnamAddressData.GetProvinces();
        }
    }

    public async Task<List<CommuneDto>> GetCommunesByProvinceAsync(string provinceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"latest/provinces/{provinceId}/communes");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<AddressKitCommunesResponse>(json, JsonOptions);
            var communes = wrapper?.Communes;

            if (communes == null || communes.Count == 0)
                return new List<CommuneDto>();

            return communes
                .OrderBy(c => c.Name, StringComparer.Create(new CultureInfo("vi-VN"), false))
                .ToList();
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("API request timed out after 10 seconds");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to fetch communes for province {provinceId}: {ex.Message}", ex);
        }
    }

    public string ComposeFullAddress(AddressComponentsDto components)
    {
        if (components == null)
            throw new ArgumentNullException(nameof(components));

        return $"{components.StreetAddress}, {components.CommuneName}, {components.ProvinceName}";
    }

    public string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        result = result.Replace('đ', 'd').Replace('Đ', 'D');

        return result;
    }

    public string SanitizeStreetAddress(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        var result = Regex.Replace(input, @"<[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"javascript\s*:", string.Empty, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s*on\w+\s*=\s*['""][^'""]*['""]", string.Empty, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s*on\w+\s*=\s*\S+", string.Empty, RegexOptions.IgnoreCase);

        return result.Trim();
    }

    public IEnumerable<T> FilterByKeyword<T>(IEnumerable<T> items, string? keyword, Func<T, string> nameSelector)
    {
        if (items == null)
            return Enumerable.Empty<T>();

        if (string.IsNullOrWhiteSpace(keyword))
            return items;

        var normalizedKeyword = RemoveDiacritics(keyword).ToLowerInvariant();

        return items.Where(item =>
        {
            var name = nameSelector(item);
            if (string.IsNullOrEmpty(name))
                return false;

            var normalizedName = RemoveDiacritics(name).ToLowerInvariant();
            return normalizedName.Contains(normalizedKeyword);
        });
    }
}
