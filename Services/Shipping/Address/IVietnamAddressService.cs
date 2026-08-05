using Fruitables.Models;

namespace Fruitables.Services.Shipping.Address;

public interface IVietnamAddressService
{
    Task<List<ProvinceDto>> GetProvincesAsync();

    Task<List<CommuneDto>> GetCommunesByProvinceAsync(string provinceId);

    string ComposeFullAddress(AddressComponentsDto components);

    string RemoveDiacritics(string text);

    string SanitizeStreetAddress(string? input);

    IEnumerable<T> FilterByKeyword<T>(IEnumerable<T> items, string? keyword, Func<T, string> nameSelector);
}
