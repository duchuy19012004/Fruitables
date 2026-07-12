namespace Fruitables.Constants;

/// <summary>
/// Setting keys allowed into the RAG knowledge index (public store/contact/shipping info only).
/// </summary>
public static class ChatSettingAllowlist
{
    public static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        SettingKeys.SiteName,
        SettingKeys.ContactAddress,
        SettingKeys.ContactPhone,
        SettingKeys.ContactEmail,
        SettingKeys.ContactWorkingHours,
        SettingKeys.ShippingFeeZone1,
        SettingKeys.ShippingFeeZone2,
        SettingKeys.ShippingFeeZone3,
        SettingKeys.FreeShippingThreshold,
    };
}
