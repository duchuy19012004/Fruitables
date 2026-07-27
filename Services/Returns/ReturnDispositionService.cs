using Fruitables.Data;
using Fruitables.Helpers;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class ReturnDispositionService : IReturnDispositionService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    public ReturnDispositionService(ApplicationDbContext db, TimeProvider clock) { _db = db; _clock = clock; }

    public async Task<(bool Success, string? Error, InventoryDisposition? Disposition)> RecordAsync(int returnItemId, int quantity, InventoryDispositionType disposition, int inspectorId, string notes, bool canOverridePolicy, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(notes)) return (false, "Số lượng và ghi chú kiểm định là bắt buộc.", null);
        var item = await _db.ReturnRequestItems.Include(x => x.OrderItem).Include(x => x.ReturnPolicy).SingleOrDefaultAsync(x => x.Id == returnItemId, cancellationToken);
        if (item == null || quantity > item.ApprovedQuantity) return (false, "Số lượng xử lý hàng không hợp lệ.", null);
        if (disposition == InventoryDispositionType.Restocked && (item.ReturnPolicy?.AllowRestock != true || !canOverridePolicy)) return (false, "Sản phẩm không được phép hoàn kho hoặc tài khoản thiếu quyền phê duyệt cấp cao.", null);
        var recorded = await _db.InventoryDispositions.Where(x => x.ReturnRequestItemId == returnItemId).SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;
        if (recorded + quantity > item.ApprovedQuantity) return (false, "Tổng số lượng xử lý vượt số lượng đã duyệt.", null);

        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        var entity = new InventoryDisposition { ReturnRequestItemId = returnItemId, Quantity = quantity, Disposition = disposition, InspectorUserId = inspectorId, Notes = notes.Trim(), CreatedAtUtc = _clock.GetUtcNow().UtcDateTime };
        _db.InventoryDispositions.Add(entity);
        if (disposition == InventoryDispositionType.Restocked)
        {
            if (item.OrderItem.ProductVariantId.HasValue)
                await _db.ProductVariants.Where(x => x.Id == item.OrderItem.ProductVariantId).ExecuteUpdateAsync(x => x.SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantity), cancellationToken);
            else
                await _db.Products.Where(x => x.Id == item.OrderItem.ProductId).ExecuteUpdateAsync(x => x.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity), cancellationToken);
        }
        _db.ReturnEvents.Add(new ReturnEvent { ReturnRequestId = item.ReturnRequestId, Type = ReturnEventType.DispositionRecorded, ActorUserId = inspectorId, Note = $"{ReturnDisplay.Text(disposition)}: {quantity}. {notes.Trim()}", CreatedAtUtc = entity.CreatedAtUtc });
        await _db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return (true, null, entity);
    }
}
