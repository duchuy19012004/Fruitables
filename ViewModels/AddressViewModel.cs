namespace Fruitables.ViewModels;

/// <summary>
/// ViewModel for displaying addresses in dropdowns and lists
/// </summary>
public class AddressViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    
    // Structured address fields
    public string ProvinceCode { get; set; } = string.Empty;
    public string ProvinceName { get; set; } = string.Empty;
    public string CommuneCode { get; set; } = string.Empty;
    public string CommuneName { get; set; } = string.Empty;
    public int? GhnDistrictId { get; set; }
    public string? GhnWardCode { get; set; }
    public string StreetAddress { get; set; } = string.Empty;
    
    public bool IsDefault { get; set; }

    public string FullAddress => $"{StreetAddress}, {CommuneName}, {ProvinceName}";

    public string DisplayText => $"{FullName} - {Phone}";

    public string ShortAddress => FullAddress.Length > 50
        ? FullAddress.Substring(0, 47) + "..."
        : FullAddress;
}
