using System.Text.Json.Serialization;

namespace Fruitables.Models;

public class ProvinceDto
{
    [JsonPropertyName("code")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}

public class CommuneDto
{
    [JsonPropertyName("code")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("provinceCode")]
    public string ProvinceId { get; set; } = null!;
}

public class AddressKitProvincesResponse
{
    [JsonPropertyName("provinces")]
    public List<ProvinceDto> Provinces { get; set; } = new();
}

public class AddressKitCommunesResponse
{
    [JsonPropertyName("communes")]
    public List<CommuneDto> Communes { get; set; } = new();
}

public class AddressComponentsDto
{
    public string ProvinceCode { get; set; } = null!;
    public string ProvinceName { get; set; } = null!;

    public string CommuneCode { get; set; } = null!;
    public string CommuneName { get; set; } = null!;

    public string StreetAddress { get; set; } = null!;
}

public class ConvertAddressRequest
{
    [JsonPropertyName("provinceCode")]
    public int ProvinceCode { get; set; }

    [JsonPropertyName("districtCode")]
    public int DistrictCode { get; set; }

    [JsonPropertyName("wardCode")]
    public int WardCode { get; set; }
}

public class ConvertAddressResponse
{
    [JsonPropertyName("provinceId")]
    public string ProvinceId { get; set; } = string.Empty;

    [JsonPropertyName("provinceName")]
    public string ProvinceName { get; set; } = string.Empty;

    [JsonPropertyName("communeId")]
    public string CommuneId { get; set; } = string.Empty;

    [JsonPropertyName("communeName")]
    public string CommuneName { get; set; } = string.Empty;
}
