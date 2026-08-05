namespace Fruitables.Services.Shipping.Providers;

public interface IGhnService
{
    Task<decimal?> CalculateFeeAsync(
        int toDistrictId,
        string toWardCode,
        int weight,
        int length,
        int width,
        int height,
        CancellationToken cancellationToken = default);

    Task<GhnAddressCode?> ResolveAddressAsync(
        string provinceName,
        string wardOrCommuneName,
        CancellationToken cancellationToken = default);
}

public sealed record GhnAddressCode(int DistrictId, string WardCode);
