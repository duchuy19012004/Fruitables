namespace Fruitables.Services.Catalog.Products;

public interface IProductLogService
{
    Task LogCreateAsync(int productId, int adminId, string productName);
    Task LogUpdateAsync(int productId, int adminId, string changes);
    Task LogDeleteAsync(int productId, int adminId, bool isHardDelete);
    Task LogRestoreAsync(int productId, int adminId);
    Task LogImageUploadAsync(int productId, int adminId, string fileName);
    Task LogImageDeleteAsync(int productId, int adminId, string fileName);
    Task LogTagUpdateAsync(int productId, int adminId, string tagNames);
    Task LogVariantCreateAsync(int productId, int adminId, string variantName);
    Task LogVariantUpdateAsync(int productId, int adminId, string variantName);
    Task LogVariantDeleteAsync(int productId, int adminId, string variantName);
    Task LogErrorAsync(string action, int? productId, Exception ex);
}
