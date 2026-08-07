using System.Text.Json;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Auditing;

namespace Fruitables.Services.Catalog.Products;

public class ProductLogService : IProductLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter? _auditLogWriter;

    public ProductLogService(IUnitOfWork unitOfWork, IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    public async Task LogCreateAsync(int productId, int adminId, string productName)
    {
        await WriteAsync(ProductLogActions.Create, productId, adminId, $"Tạo sản phẩm: {productName}");
    }

    public async Task LogUpdateAsync(int productId, int adminId, string changes)
    {
        await WriteAsync(ProductLogActions.Update, productId, adminId, $"Cập nhật sản phẩm: {changes}");
    }

    public async Task LogDeleteAsync(int productId, int adminId, bool isHardDelete)
    {
        await WriteAsync(
            isHardDelete ? ProductLogActions.HardDelete : ProductLogActions.SoftDelete,
            productId,
            adminId,
            isHardDelete ? "Xóa vĩnh viễn sản phẩm" : "Chuyển sản phẩm vào thùng rác");
    }

    public async Task LogRestoreAsync(int productId, int adminId)
    {
        await WriteAsync(ProductLogActions.Restore, productId, adminId, "Khôi phục sản phẩm từ thùng rác");
    }

    public async Task LogImageUploadAsync(int productId, int adminId, string fileName)
    {
        await WriteAsync(ProductLogActions.ImageUpload, productId, adminId, $"Upload ảnh: {fileName}");
    }

    public async Task LogImageDeleteAsync(int productId, int adminId, string fileName)
    {
        await WriteAsync(ProductLogActions.ImageDelete, productId, adminId, $"Xóa ảnh: {fileName}");
    }

    public async Task LogTagUpdateAsync(int productId, int adminId, string tagNames)
    {
        await WriteAsync(ProductLogActions.TagUpdate, productId, adminId, $"Cập nhật tags: {tagNames}");
    }

    public async Task LogVariantCreateAsync(int productId, int adminId, string variantName)
    {
        await WriteAsync(ProductLogActions.VariantCreate, productId, adminId, $"Tạo biến thể: {variantName}");
    }

    public async Task LogVariantUpdateAsync(int productId, int adminId, string variantName)
    {
        await WriteAsync(ProductLogActions.VariantUpdate, productId, adminId, $"Cập nhật biến thể: {variantName}");
    }

    public async Task LogVariantDeleteAsync(int productId, int adminId, string variantName)
    {
        await WriteAsync(ProductLogActions.VariantDelete, productId, adminId, $"Xóa biến thể: {variantName}");
    }

    public async Task LogErrorAsync(string action, int? productId, Exception ex)
    {
        await WriteAsync(ProductLogActions.Error, productId ?? 0, 0, $"Lỗi khi {action}: {ex.Message}");
    }

    private Task WriteAsync(string action, int entityId, int adminId, string details) =>
        _auditLogWriter?.WriteAsync(
            action,
            "Product",
            entityId,
            adminId,
            newValue: JsonSerializer.Serialize(new { details })) ?? Task.CompletedTask;
}
