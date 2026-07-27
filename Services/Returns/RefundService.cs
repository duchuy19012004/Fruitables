using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Outbox;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class RefundService : IRefundService
{
    private const string DestinationPurpose = "Fruitables.Returns.RefundDestination.v1";
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IDataProtector _destinationProtector;
    private readonly IOutboxService _outbox;

    public RefundService(
        ApplicationDbContext db,
        TimeProvider clock,
        IDataProtectionProvider dataProtection,
        IOutboxService? outbox = null)
    {
        _db = db;
        _clock = clock;
        _destinationProtector = dataProtection.CreateProtector(DestinationPurpose);
        _outbox = outbox ?? new OutboxService(db, clock);
    }

    public async Task<(bool Success, string? Error)> SaveDestinationAsync(
        int refundId,
        int customerId,
        RefundDestinationInputViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true))
            return (false, "Thông tin nhận tiền không hợp lệ.");

        var bankCode = model.BankCode.Trim().ToUpperInvariant();
        var accountNumber = model.AccountNumber.Replace(" ", string.Empty);
        var accountHolder = model.AccountHolder.Trim().ToUpperInvariant();
        var numberProtected = _destinationProtector.Protect(accountNumber);
        var holderProtected = _destinationProtector.Protect(accountHolder);
        var accountLast4 = accountNumber[^4..];
        var now = _clock.GetUtcNow().UtcDateTime;

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var refund = await _db.Refunds
            .Where(x => x.Id == refundId
                && x.ReturnRequestItemId == null
                && x.ReturnRequest.UserId == customerId
                && (x.Status == RefundStatus.AwaitingDestination || x.Status == RefundStatus.AwaitingApproval))
            .SingleOrDefaultAsync(cancellationToken);
        if (refund == null) return (false, "Khoản hoàn không thể cập nhật thông tin nhận tiền.");

        var changed = await _db.Refunds
            .Where(x => x.Id == refundId
                && (x.Status == RefundStatus.AwaitingDestination || x.Status == RefundStatus.AwaitingApproval))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DestinationBankCode, bankCode)
                .SetProperty(x => x.DestinationAccountNumberProtected, numberProtected)
                .SetProperty(x => x.DestinationAccountLast4, accountLast4)
                .SetProperty(x => x.DestinationAccountHolderProtected, holderProtected)
                .SetProperty(x => x.DestinationSubmittedAtUtc, now)
                .SetProperty(x => x.Status, RefundStatus.AwaitingApproval), cancellationToken);
        if (changed != 1)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            return (false, "Dữ liệu vừa thay đổi, vui lòng thử lại.");
        }

        refund.DestinationBankCode = bankCode;
        refund.DestinationAccountNumberProtected = numberProtected;
        refund.DestinationAccountLast4 = accountLast4;
        refund.DestinationAccountHolderProtected = holderProtected;
        refund.DestinationSubmittedAtUtc = now;
        refund.Status = RefundStatus.AwaitingApproval;
        AcceptExecutedUpdate(refund);
        _db.ReturnEvents.Add(NewEvent(
            refund.ReturnRequestId,
            ReturnEventType.RefundDestinationSubmitted,
            ReturnRequestStatus.ResolutionPending,
            ReturnRequestStatus.ResolutionPending,
            customerId,
            "Khách hàng đã cung cấp thông tin nhận tiền.",
            now));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            return (false, "Dữ liệu vừa thay đổi, vui lòng thử lại.");
        }
    }

    public async Task<List<RefundQueueItemViewModel>> GetQueueAsync(
        RefundQueueFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Refunds.AsNoTracking().Where(x => x.ReturnRequestItemId == null);
        query = filter.Bucket switch
        {
            RefundQueueBucket.WaitingCustomer => query.Where(x => x.Status == RefundStatus.AwaitingDestination),
            RefundQueueBucket.Ready => query.Where(x => x.Status == RefundStatus.AwaitingApproval),
            RefundQueueBucket.Working => query.Where(x => x.Status == RefundStatus.Processing || x.Status == RefundStatus.Failed),
            RefundQueueBucket.Completed => query.Where(x => x.Status == RefundStatus.Succeeded || x.Status == RefundStatus.Cancelled),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.ReturnRequest.ReturnNumber.Contains(search)
                || x.Order.OrderNumber.Contains(search)
                || x.ReturnRequest.User.Email.Contains(search));
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        return await query
            .OrderBy(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RefundQueueItemViewModel
            {
                RefundId = x.Id,
                ReturnRequestId = x.ReturnRequestId,
                ReturnNumber = x.ReturnRequest.ReturnNumber,
                OrderNumber = x.Order.OrderNumber,
                CustomerName = x.ReturnRequest.User.Name,
                Amount = x.Amount,
                Status = x.Status,
                BankCode = x.DestinationBankCode,
                AccountLast4 = x.DestinationAccountLast4,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, string? Error, FinanceRefundViewModel? Data)> GetFinanceTaskAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default)
    {
        var refund = await _db.Refunds
            .Include(x => x.ReturnRequest).ThenInclude(x => x.User)
            .Include(x => x.Order)
            .SingleOrDefaultAsync(x => x.Id == refundId && x.ReturnRequestItemId == null, cancellationToken);
        if (refund == null) return (false, "Không tìm thấy khoản hoàn.", null);

        string? accountNumber = null;
        string? accountHolder = null;
        if (refund.Status is RefundStatus.AwaitingApproval or RefundStatus.Processing or RefundStatus.Failed)
        {
            if (refund.DestinationAccountNumberProtected == null || refund.DestinationAccountHolderProtected == null)
                return (false, "Không thể đọc thông tin nhận tiền.", null);
            try
            {
                accountNumber = _destinationProtector.Unprotect(refund.DestinationAccountNumberProtected);
                accountHolder = _destinationProtector.Unprotect(refund.DestinationAccountHolderProtected);
            }
            catch (CryptographicException)
            {
                return (false, "Không thể đọc thông tin nhận tiền.", null);
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        _db.ReturnEvents.Add(NewEvent(
            refund.ReturnRequestId,
            ReturnEventType.RefundDestinationViewed,
            refund.ReturnRequest.Status,
            refund.ReturnRequest.Status,
            financeUserId,
            "Bộ phận tài chính đã xem thông tin nhận tiền.",
            now));
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null, new FinanceRefundViewModel
        {
            RefundId = refund.Id,
            ReturnRequestId = refund.ReturnRequestId,
            ReturnNumber = refund.ReturnRequest.ReturnNumber,
            OrderNumber = refund.Order.OrderNumber,
            CustomerName = refund.ReturnRequest.User.Name,
            Amount = refund.Amount,
            Status = refund.Status,
            BankCode = refund.DestinationBankCode,
            AccountNumber = accountNumber,
            AccountHolder = accountHolder,
            AccountLast4 = refund.DestinationAccountLast4
        });
    }

    public async Task<(bool Success, string? Error)> StartProcessingAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _db.Refunds.AsNoTracking()
            .Where(x => x.Id == refundId && x.ReturnRequestItemId == null)
            .Select(x => new { x.ReturnRequestId, x.Amount, x.CreatedByUserId, RequestStatus = x.ReturnRequest.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate == null) return (false, "Khoản hoàn không thể bắt đầu xử lý.");
        if (candidate.Amount >= 500_000m && candidate.CreatedByUserId == financeUserId)
            return (false, "Khoản hoàn từ 500.000₫ phải được người khác xử lý.");

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var changed = await _db.Refunds
            .Where(x => x.Id == refundId
                && x.ReturnRequestItemId == null
                && (x.Status == RefundStatus.AwaitingApproval || x.Status == RefundStatus.Failed)
                && x.DestinationAccountNumberProtected != null
                && x.DestinationAccountHolderProtected != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RefundStatus.Processing)
                .SetProperty(x => x.ProcessedByUserId, financeUserId), cancellationToken);
        if (changed != 1)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            return (false, "Khoản hoàn đã được người khác nhận xử lý hoặc chưa đủ thông tin.");
        }

        var tracked = _db.Refunds.Local.FirstOrDefault(x => x.Id == refundId);
        if (tracked != null)
        {
            tracked.Status = RefundStatus.Processing;
            tracked.ProcessedByUserId = financeUserId;
            AcceptExecutedUpdate(tracked);
        }
        _db.ReturnEvents.Add(NewEvent(
            candidate.ReturnRequestId,
            ReturnEventType.RefundProcessingStarted,
            candidate.RequestStatus,
            candidate.RequestStatus,
            financeUserId,
            "Bộ phận tài chính bắt đầu xử lý.",
            _clock.GetUtcNow().UtcDateTime));
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> FailAsync(
        int refundId,
        int financeUserId,
        RefundFailureInputViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true))
            return (false, "Lý do thất bại không hợp lệ.");

        var candidate = await _db.Refunds.AsNoTracking()
            .Where(x => x.Id == refundId
                && x.ReturnRequestItemId == null
                && x.Status == RefundStatus.Processing
                && x.ProcessedByUserId == financeUserId)
            .Select(x => new { x.ReturnRequestId, RequestStatus = x.ReturnRequest.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate == null) return (false, "Khoản hoàn không thể ghi nhận thất bại.");

        var reason = model.Reason.Trim();
        var now = _clock.GetUtcNow().UtcDateTime;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var guarded = _db.Refunds.Where(x => x.Id == refundId
            && x.Status == RefundStatus.Processing
            && x.ProcessedByUserId == financeUserId);
        var changed = model.RequestCustomerCorrection
            ? await guarded.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RefundStatus.AwaitingDestination)
                .SetProperty(x => x.ProcessedByUserId, (int?)null)
                .SetProperty(x => x.FailureReason, reason)
                .SetProperty(x => x.DestinationBankCode, (string?)null)
                .SetProperty(x => x.DestinationAccountNumberProtected, (string?)null)
                .SetProperty(x => x.DestinationAccountLast4, (string?)null)
                .SetProperty(x => x.DestinationAccountHolderProtected, (string?)null)
                .SetProperty(x => x.DestinationSubmittedAtUtc, (DateTime?)null), cancellationToken)
            : await guarded.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RefundStatus.Failed)
                .SetProperty(x => x.FailureReason, reason), cancellationToken);
        if (changed != 1)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            return (false, "Dữ liệu vừa thay đổi, vui lòng thử lại.");
        }

        var request = await _db.ReturnRequests.SingleAsync(x => x.Id == candidate.ReturnRequestId, cancellationToken);
        request.Status = model.RequestCustomerCorrection
            ? ReturnRequestStatus.ResolutionPending
            : ReturnRequestStatus.ResolutionFailed;
        var tracked = _db.Refunds.Local.FirstOrDefault(x => x.Id == refundId);
        if (tracked != null)
        {
            tracked.Status = model.RequestCustomerCorrection ? RefundStatus.AwaitingDestination : RefundStatus.Failed;
            tracked.FailureReason = reason;
            if (model.RequestCustomerCorrection)
            {
                tracked.ProcessedByUserId = null;
                tracked.DestinationBankCode = null;
                tracked.DestinationAccountNumberProtected = null;
                tracked.DestinationAccountLast4 = null;
                tracked.DestinationAccountHolderProtected = null;
                tracked.DestinationSubmittedAtUtc = null;
            }
            AcceptExecutedUpdate(tracked);
        }
        _db.ReturnEvents.Add(NewEvent(
            candidate.ReturnRequestId,
            model.RequestCustomerCorrection ? ReturnEventType.RefundDestinationCorrectionRequested : ReturnEventType.RefundFailed,
            candidate.RequestStatus,
            request.Status,
            financeUserId,
            reason,
            now));
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ConfirmManualAsync(
        int refundId,
        string transactionReference,
        string transferEvidenceStorageKey,
        int financeUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionReference) || string.IsNullOrWhiteSpace(transferEvidenceStorageKey))
            return (false, "Cần mã giao dịch và bằng chứng chuyển tiền.");
        var reference = transactionReference.Trim();
        var evidenceKey = Path.GetFileName(transferEvidenceStorageKey);
        if (string.IsNullOrWhiteSpace(evidenceKey)) return (false, "Bằng chứng chuyển tiền không hợp lệ.");

        var refund = await _db.Refunds
            .Include(x => x.Order)
            .Include(x => x.ReturnRequest)
            .SingleOrDefaultAsync(x => x.Id == refundId && x.ReturnRequestItemId == null, cancellationToken);
        if (refund == null
            || refund.Method != RefundMethod.ManualBankTransfer
            || refund.Status != RefundStatus.Processing
            || refund.ProcessedByUserId != financeUserId
            || refund.DestinationAccountNumberProtected == null
            || refund.DestinationAccountHolderProtected == null)
            return (false, "Khoản hoàn không thể xác nhận.");
        if (refund.Amount >= 500_000m && refund.CreatedByUserId == financeUserId)
            return (false, "Khoản hoàn từ 500.000₫ phải được người khác xác nhận.");
        if (await _db.Refunds.AnyAsync(x => x.Id != refundId && x.TransactionReference == reference, cancellationToken))
            return (false, "Mã giao dịch đã được sử dụng.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var previousRequestStatus = refund.ReturnRequest.Status;
        refund.Status = RefundStatus.Succeeded;
        refund.TransactionReference = reference;
        refund.TransferEvidenceStorageKey = evidenceKey;
        refund.ProcessedAtUtc = now;
        refund.DestinationAccountNumberProtected = null;
        refund.DestinationAccountHolderProtected = null;

        var succeeded = (await _db.Refunds
            .Where(x => x.OrderId == refund.OrderId && x.Status == RefundStatus.Succeeded && x.Id != refund.Id)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken)).Sum() + refund.Amount;
        refund.Order.PaymentStatus = succeeded >= refund.Order.Total
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;
        refund.ReturnRequest.Status = ReturnRequestStatus.Resolved;
        refund.ReturnRequest.ResolvedAtUtc = now;
        _db.ReturnEvents.Add(NewEvent(
            refund.ReturnRequestId,
            ReturnEventType.RefundSucceeded,
            previousRequestStatus,
            ReturnRequestStatus.Resolved,
            financeUserId,
            "Bộ phận tài chính xác nhận hoàn tiền thủ công.",
            now));
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.RefundSucceeded,
            new
            {
                refundId = refund.Id,
                refund.ReturnRequestId,
                refund.OrderId,
                refund.Amount,
                requestStatus = refund.ReturnRequest.Status.ToString()
            },
            $"refund:{refund.Id}:succeeded",
            cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Mã giao dịch đã được sử dụng hoặc dữ liệu vừa thay đổi.");
        }
    }

    private void AcceptExecutedUpdate(Refund refund)
    {
        var entry = _db.Entry(refund);
        entry.OriginalValues.SetValues(entry.CurrentValues);
    }

    private static ReturnEvent NewEvent(
        int requestId,
        ReturnEventType type,
        ReturnRequestStatus? from,
        ReturnRequestStatus? to,
        int actorId,
        string note,
        DateTime now) => new()
        {
            ReturnRequestId = requestId,
            Type = type,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actorId,
            Note = note,
            CreatedAtUtc = now
        };
}
