using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class RefundService : IRefundService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IOutboxService _outbox;
    public RefundService(ApplicationDbContext db, TimeProvider clock, IOutboxService? outbox = null)
    {
        _db = db;
        _clock = clock;
        _outbox = outbox ?? new OutboxService(db, clock);
    }

    public async Task<(bool Success, string? Error, Refund? Refund)> CreateAsync(int returnRequestId, int returnRequestItemId, decimal amount, RefundMethod method, string idempotencyKey, int adminId, CancellationToken cancellationToken = default)
    {
        var duplicate = await _db.Refunds.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (duplicate != null) return (true, null, duplicate);
        var request = await _db.ReturnRequests.Include(x => x.Items).Include(x => x.Order).SingleOrDefaultAsync(x => x.Id == returnRequestId, cancellationToken);
        if (request == null || request.Status is not (ReturnRequestStatus.Approved or ReturnRequestStatus.PartiallyApproved or ReturnRequestStatus.ResolutionPending or ReturnRequestStatus.ResolutionFailed)) return (false, "Yêu cầu chưa được duyệt để hoàn tiền.", null);
        var isShippingFee = returnRequestItemId == 0;
        var returnItem = isShippingFee ? null : request.Items.SingleOrDefault(x => x.Id == returnRequestItemId);
        if (isShippingFee && !request.ShippingFeeApproved) return (false, "Phí vận chuyển chưa được duyệt hoàn.", null);
        if (!isShippingFee && (returnItem == null || returnItem.ApprovedQuantity <= 0)) return (false, "Sản phẩm không thuộc phần đã duyệt.", null);
        var approvedCap = isShippingFee ? request.Order.ShippingFee : returnItem!.ApprovedAmount;
        var reserved = (await _db.Refunds.Where(x => x.ReturnRequestId == returnRequestId && x.ReturnRequestItemId == (isShippingFee ? null : returnRequestItemId) && x.Status != RefundStatus.Failed && x.Status != RefundStatus.Cancelled).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum();
        var orderSucceeded = (await _db.Refunds.Where(x => x.OrderId == request.OrderId && x.Status == RefundStatus.Succeeded).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum();
        if (amount <= 0 || amount > approvedCap - reserved || amount > request.Order.Total - orderSucceeded) return (false, "Số tiền hoàn vượt hạn mức còn lại.", null);
        var now = _clock.GetUtcNow().UtcDateTime;
        var refund = new Refund { ReturnRequestId = request.Id, ReturnRequestItemId = returnItem?.Id, OrderId = request.OrderId, Amount = amount, Method = method, Status = method == RefundMethod.ManualBankTransfer ? RefundStatus.Pending : RefundStatus.AwaitingApproval, IdempotencyKey = idempotencyKey, CreatedByUserId = adminId, CreatedAtUtc = now };
        _db.Refunds.Add(refund);
        var previousStatus = request.Status;
        request.Status = ReturnRequestStatus.ResolutionPending;
        _db.ReturnEvents.Add(new ReturnEvent { ReturnRequestId = request.Id, Type = ReturnEventType.RefundCreated, FromStatus = previousStatus, ToStatus = ReturnRequestStatus.ResolutionPending, ActorUserId = adminId, Note = $"Tạo khoản hoàn {amount:N0}₫.", CreatedAtUtc = now });
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.RefundCreated,
            new { returnRequestId = request.Id, orderId = request.OrderId, amount, method = method.ToString(), refundIdempotencyKey = idempotencyKey },
            $"refund:{idempotencyKey}:created",
            cancellationToken);
        try { await _db.SaveChangesAsync(cancellationToken); return (true, null, refund); }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var existing = await _db.Refunds.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing != null) return (true, null, existing);
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> ConfirmManualAsync(int refundId, string transactionReference, string transferEvidenceStorageKey, int financeUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionReference) || string.IsNullOrWhiteSpace(transferEvidenceStorageKey)) return (false, "Cần mã giao dịch và bằng chứng chuyển tiền.");
        var refund = await _db.Refunds.Include(x => x.Order).Include(x => x.ReturnRequest).ThenInclude(x => x.Items).SingleOrDefaultAsync(x => x.Id == refundId, cancellationToken);
        if (refund == null || refund.Method != RefundMethod.ManualBankTransfer || refund.Status is RefundStatus.Succeeded or RefundStatus.Cancelled) return (false, "Khoản hoàn không thể xác nhận.");
        if (refund.Amount >= 500_000m && refund.CreatedByUserId == financeUserId) return (false, "Khoản hoàn từ 500.000₫ phải được người khác xác nhận.");
        if (await _db.Refunds.AnyAsync(x => x.Id != refundId && x.TransactionReference == transactionReference, cancellationToken)) return (false, "Mã giao dịch đã được sử dụng.");
        var now = _clock.GetUtcNow().UtcDateTime;
        refund.Status = RefundStatus.Succeeded;
        refund.TransactionReference = transactionReference.Trim();
        refund.TransferEvidenceStorageKey = Path.GetFileName(transferEvidenceStorageKey);
        refund.ProcessedByUserId = financeUserId;
        refund.ProcessedAtUtc = now;
        var succeeded = (await _db.Refunds.Where(x => x.OrderId == refund.OrderId && x.Status == RefundStatus.Succeeded && x.Id != refund.Id).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum() + refund.Amount;
        refund.Order.PaymentStatus = succeeded >= refund.Order.Total ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        var requestPaid = (await _db.Refunds.Where(x => x.ReturnRequestId == refund.ReturnRequestId && x.Status == RefundStatus.Succeeded && x.Id != refund.Id).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum() + refund.Amount;
        var target = refund.ReturnRequest.Items.Sum(x => x.ApprovedAmount) + (refund.ReturnRequest.ShippingFeeApproved ? refund.Order.ShippingFee : 0);
        if (requestPaid >= target) { refund.ReturnRequest.Status = ReturnRequestStatus.Resolved; refund.ReturnRequest.ResolvedAtUtc = now; }
        _db.ReturnEvents.Add(new ReturnEvent { ReturnRequestId = refund.ReturnRequestId, Type = ReturnEventType.RefundSucceeded, FromStatus = ReturnRequestStatus.ResolutionPending, ToStatus = refund.ReturnRequest.Status, ActorUserId = financeUserId, Note = "Bộ phận tài chính xác nhận hoàn tiền thủ công.", CreatedAtUtc = now });
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.RefundSucceeded,
            new { refundId = refund.Id, refund.ReturnRequestId, refund.OrderId, refund.Amount, requestStatus = refund.ReturnRequest.Status.ToString() },
            $"refund:{refund.Id}:succeeded",
            cancellationToken);
        try { await _db.SaveChangesAsync(cancellationToken); return (true, null); }
        catch (DbUpdateException) { return (false, "Mã giao dịch đã được sử dụng hoặc dữ liệu vừa thay đổi."); }
    }
}
