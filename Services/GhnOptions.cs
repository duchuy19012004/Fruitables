namespace Fruitables.Services;

public class GhnOptions
{
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/";
    public string Token { get; set; } = string.Empty;
    public int ShopId { get; set; }
    public int FromDistrictId { get; set; }
    public string FromWardCode { get; set; } = string.Empty;
    public int ServiceTypeId { get; set; } = 2;
    public int DefaultWeight { get; set; } = 1000;
    public int DefaultLength { get; set; } = 20;
    public int DefaultWidth { get; set; } = 15;
    public int DefaultHeight { get; set; } = 10;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token)
        && ShopId > 0
        && FromDistrictId > 0
        && !string.IsNullOrWhiteSpace(FromWardCode);
}
