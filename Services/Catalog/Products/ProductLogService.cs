using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Catalog.Products;

public class ProductLogService : IProductLogService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductLogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogCreateAsync(int productId, int adminId, string productName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.Create,
            Details = $"Tạo sản phẩm: {productName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogUpdateAsync(int productId, int adminId, string changes)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.Update,
            Details = $"Cập nhật sản phẩm: {changes}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogDeleteAsync(int productId, int adminId, bool isHardDelete)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = isHardDelete ? ProductLogActions.HardDelete : ProductLogActions.SoftDelete,
            Details = isHardDelete ? "Xóa vĩnh viễn sản phẩm" : "Chuyển sản phẩm vào thùng rác",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogRestoreAsync(int productId, int adminId)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.Restore,
            Details = "Khôi phục sản phẩm từ thùng rác",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogImageUploadAsync(int productId, int adminId, string fileName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.ImageUpload,
            Details = $"Upload ảnh: {fileName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogImageDeleteAsync(int productId, int adminId, string fileName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.ImageDelete,
            Details = $"Xóa ảnh: {fileName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogTagUpdateAsync(int productId, int adminId, string tagNames)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.TagUpdate,
            Details = $"Cập nhật tags: {tagNames}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogVariantCreateAsync(int productId, int adminId, string variantName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.VariantCreate,
            Details = $"Tạo biến thể: {variantName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogVariantUpdateAsync(int productId, int adminId, string variantName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.VariantUpdate,
            Details = $"Cập nhật biến thể: {variantName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogVariantDeleteAsync(int productId, int adminId, string variantName)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = adminId,
            Action = ProductLogActions.VariantDelete,
            Details = $"Xóa biến thể: {variantName}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogErrorAsync(string action, int? productId, Exception ex)
    {
        var log = new ProductLog
        {
            ProductId = productId,
            AdminId = 0, // System error
            Action = ProductLogActions.Error,
            Details = $"Lỗi khi {action}: {ex.Message}",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ProductLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }
}
