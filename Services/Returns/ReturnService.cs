using System.Data;
using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Outbox;
using Fruitables.ViewModels.Returns;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fruitables.Services.Returns;

public class ReturnService : IReturnService
{
    private static readonly IReadOnlyDictionary<ReturnRequestStatus, ReturnRequestStatus[]> Transitions = new Dictionary<ReturnRequestStatus, ReturnRequestStatus[]>
    {
        [ReturnRequestStatus.Submitted] = [ReturnRequestStatus.AwaitingEvidence, ReturnRequestStatus.UnderReview, ReturnRequestStatus.Cancelled],
        [ReturnRequestStatus.AwaitingEvidence] = [ReturnRequestStatus.UnderReview, ReturnRequestStatus.Expired, ReturnRequestStatus.Cancelled],
        [ReturnRequestStatus.UnderReview] = [ReturnRequestStatus.Approved, ReturnRequestStatus.PartiallyApproved, ReturnRequestStatus.Rejected],
        [ReturnRequestStatus.Approved] = [ReturnRequestStatus.ResolutionPending],
        [ReturnRequestStatus.PartiallyApproved] = [ReturnRequestStatus.ResolutionPending],
        [ReturnRequestStatus.ResolutionPending] = [ReturnRequestStatus.Resolved, ReturnRequestStatus.ResolutionFailed],
        [ReturnRequestStatus.ResolutionFailed] = [ReturnRequestStatus.ResolutionPending]
    };

    private readonly ApplicationDbContext _db;
    private readonly IReturnEligibilityService _eligibility;
    private readonly IRefundAmountCalculator _calculator;
    private readonly TimeProvider _clock;
    private readonly IOutboxService _outbox;

    public ReturnService(ApplicationDbContext db, IReturnEligibilityService eligibility, IRefundAmountCalculator calculator, TimeProvider clock, IOutboxService? outbox = null)
    {
        _db = db;
        _eligibility = eligibility;
        _calculator = calculator;
        _clock = clock;
        _outbox = outbox ?? new OutboxService(db, clock);
    }

    public async Task<ReturnResult> SubmitAsync(int userId, ReturnSubmitViewModel model, CancellationToken cancellationToken = default)
    {
        var submittedItems = model.Items.Where(x => x.Selected).ToList();
        if (submittedItems.Count == 0) return ReturnResult.Fail("Vui lòng chọn ít nhất một sản phẩm.");
        if (submittedItems.Select(x => x.OrderItemId).Distinct().Count() != submittedItems.Count) return ReturnResult.Fail("Một sản phẩm không thể xuất hiện nhiều lần.");
        if (submittedItems.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Trim().Length < 5)) return ReturnResult.Fail("Số lượng và mô tả tối thiểu 5 ký tự là bắt buộc.");
        var existing = await _db.ReturnRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == model.IdempotencyKey, cancellationToken);
        if (existing != null) return ReturnResult.Ok(existing);

        IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational()) transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var request = new ReturnRequest
            {
                ReturnNumber = $"RT{now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
                IdempotencyKey = model.IdempotencyKey,
                OrderId = model.OrderId,
                UserId = userId,
                Status = ReturnRequestStatus.Submitted,
                CustomerNote = model.CustomerNote?.Trim(),
                SubmittedAtUtc = now,
                ReviewDueAtUtc = now.AddHours(24)
            };

            foreach (var input in submittedItems)
            {
                var check = await _eligibility.CheckItemAsync(model.OrderId, input.OrderItemId, userId, input.Reason, cancellationToken);
                if (!check.Eligible || check.Policy == null) return ReturnResult.Fail(check.Error ?? "Sản phẩm không đủ điều kiện.");
                if (input.Quantity > check.RemainingQuantity) return ReturnResult.Fail("Số lượng yêu cầu vượt quá số lượng còn có thể khiếu nại.");
                if (check.EvidenceRequired && (model.EvidenceFiles?.Count ?? 0) == 0) return ReturnResult.Fail("Lý do đã chọn yêu cầu ít nhất một ảnh hoặc video.");
                if (!check.Policy.AllowPartialRefund && !check.Policy.AllowFullRefund) return ReturnResult.Fail("Lý do này không hỗ trợ hoàn tiền.");
                var amount = await _calculator.CalculateAsync(input.OrderItemId, input.Quantity, cancellationToken);
                request.Items.Add(new ReturnRequestItem
                {
                    OrderItemId = input.OrderItemId,
                    ReturnPolicyId = check.Policy.Id,
                    RequestedQuantity = input.Quantity,
                    Reason = input.Reason,
                    RequestedResolution = ReturnResolutionType.None,
                    Description = input.Description.Trim(),
                    NetPaidAmountSnapshot = amount.NetPaidAmount,
                    RequestedAmount = amount.RefundableAmount,
                    PolicyVersionSnapshot = check.Policy.Version,
                    ClaimWindowHoursSnapshot = check.Policy.ClaimWindowHours,
                    EvidenceRequiredSnapshot = check.Policy.EvidenceRequired,
                    ClaimDeadlineAtUtcSnapshot = check.DeadlineAtUtc!.Value
                });
                request.PolicyVersion = Math.Max(request.PolicyVersion, check.Policy.Version);
                request.ClaimDeadlineAtUtc = request.ClaimDeadlineAtUtc == default || check.DeadlineAtUtc < request.ClaimDeadlineAtUtc ? check.DeadlineAtUtc!.Value : request.ClaimDeadlineAtUtc;
            }

            request.Events.Add(NewEvent(ReturnEventType.Submitted, null, request.Status, userId, "Khách hàng gửi yêu cầu.", now));
            _db.ReturnRequests.Add(request);
            await _outbox.EnqueueAsync(
                OutboxMessageTypes.ReturnSubmitted,
                new { requestNumber = request.ReturnNumber, request.OrderId, request.UserId, status = request.Status.ToString() },
                $"return:{request.ReturnNumber}:submitted",
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return ReturnResult.Ok(request);
        }
        catch (DbUpdateException ex)
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            _db.ChangeTracker.Clear();
            var duplicate = await _db.ReturnRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == model.IdempotencyKey, cancellationToken);
            if (duplicate != null) return ReturnResult.Ok(duplicate);
            if (IsSubmissionConcurrencyFailure(ex)) return ReturnResult.Fail("Số lượng có thể khiếu nại vừa được cập nhật. Vui lòng tải lại và thử lại.", true);
            throw;
        }
        catch (Exception ex) when (IsSubmissionConcurrencyFailure(ex))
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            _db.ChangeTracker.Clear();
            return ReturnResult.Fail("Số lượng có thể khiếu nại vừa được cập nhật. Vui lòng tải lại và thử lại.", true);
        }
        finally { if (transaction != null) await transaction.DisposeAsync(); }
    }

    public Task<ReturnRequest?> GetForCustomerAsync(int id, int userId, CancellationToken cancellationToken = default) => FullQuery().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    public Task<List<ReturnRequest>> GetCustomerRequestsAsync(int userId, CancellationToken cancellationToken = default) => FullQuery().Where(x => x.UserId == userId).OrderByDescending(x => x.SubmittedAtUtc).ToListAsync(cancellationToken);
    public Task<ReturnRequest?> GetForAdminAsync(int id, CancellationToken cancellationToken = default) => FullQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<ReturnRequest>> GetQueueAsync(ReturnQueueFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.ReturnRequests.AsNoTracking().Include(x => x.User).Include(x => x.Order).Include(x => x.Items).ThenInclude(x => x.OrderItem).AsQueryable();
        query = filter.Bucket switch
        {
            ReturnQueueBucket.Intake => query.Where(x => x.Status == ReturnRequestStatus.Submitted),
            ReturnQueueBucket.WaitingCustomer => query.Where(x => x.Status == ReturnRequestStatus.AwaitingEvidence),
            ReturnQueueBucket.Reviewing => query.Where(x => x.Status == ReturnRequestStatus.UnderReview),
            ReturnQueueBucket.Completed => query.Where(x =>
                x.Status == ReturnRequestStatus.Rejected ||
                x.Status == ReturnRequestStatus.ResolutionPending ||
                x.Status == ReturnRequestStatus.ResolutionFailed ||
                x.Status == ReturnRequestStatus.Resolved ||
                x.Status == ReturnRequestStatus.Cancelled ||
                x.Status == ReturnRequestStatus.Expired),
            _ => query
        };
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status);
        if (filter.Reason.HasValue) query = query.Where(x => x.Items.Any(i => i.Reason == filter.Reason));
        if (filter.FromUtc.HasValue) query = query.Where(x => x.SubmittedAtUtc >= filter.FromUtc);
        if (filter.ToUtc.HasValue) query = query.Where(x => x.SubmittedAtUtc <= filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.ReturnNumber.Contains(filter.Search) || x.Order.OrderNumber.Contains(filter.Search) || x.User.Email.Contains(filter.Search));
        return query.OrderBy(x => x.ReviewDueAtUtc).Skip((Math.Max(1, filter.Page) - 1) * Math.Clamp(filter.PageSize, 1, 100)).Take(Math.Clamp(filter.PageSize, 1, 100)).ToListAsync(cancellationToken);
    }

    public Task<ReturnResult> RequestEvidenceAsync(int id, int adminId, string note, byte[] rowVersion, CancellationToken cancellationToken = default) => TransitionAsync(id, adminId, ReturnRequestStatus.AwaitingEvidence, ReturnEventType.EvidenceRequested, note, rowVersion, cancellationToken, r => r.EvidenceDueAtUtc = _clock.GetUtcNow().UtcDateTime.AddHours(24));
    public Task<ReturnResult> StartReviewAsync(int id, int adminId, byte[] rowVersion, CancellationToken cancellationToken = default) => TransitionAsync(id, adminId, ReturnRequestStatus.UnderReview, ReturnEventType.ReviewStarted, "Bắt đầu xem xét.", rowVersion, cancellationToken);

    public async Task<ReturnResult> DecideAsync(int adminId, ReturnDecisionViewModel model, CancellationToken cancellationToken = default)
    {
        var request = await _db.ReturnRequests.Include(x => x.Order).ThenInclude(x => x.Items).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == model.ReturnRequestId, cancellationToken);
        if (request == null) return ReturnResult.Fail("Không tìm thấy yêu cầu.");
        if (request.Status != ReturnRequestStatus.UnderReview) return ReturnResult.Fail("Yêu cầu không ở trạng thái đang xem xét.");
        ApplyVersion(request, model.RowVersion);
        var decisions = model.Items.ToDictionary(x => x.ReturnRequestItemId);
        var changed = false;
        foreach (var item in request.Items)
        {
            if (!decisions.TryGetValue(item.Id, out var decision)) return ReturnResult.Fail("Thiếu quyết định cho một sản phẩm.");
            if (decision.ApprovedQuantity > item.RequestedQuantity) return ReturnResult.Fail("Số lượng duyệt vượt số lượng yêu cầu.");
            if (decision.ApprovedQuantity != item.RequestedQuantity) changed = true;
            item.ApprovedQuantity = decision.ApprovedQuantity;
            item.ApprovedAmount = decision.ApprovedQuantity == 0 ? 0 : (await _calculator.CalculateAsync(item.OrderItemId, decision.ApprovedQuantity, cancellationToken)).RefundableAmount;
        }
        if ((changed || request.Items.All(x => x.ApprovedQuantity == 0)) && string.IsNullOrWhiteSpace(model.Reason)) return ReturnResult.Fail("Bắt buộc nhập lý do khi duyệt một phần hoặc từ chối.");
        var decisionStatus = request.Items.All(x => x.ApprovedQuantity == 0) ? ReturnRequestStatus.Rejected : request.Items.All(x => x.ApprovedQuantity == x.RequestedQuantity) ? ReturnRequestStatus.Approved : ReturnRequestStatus.PartiallyApproved;
        request.DecisionReason = model.Reason?.Trim();
        request.MerchantFault = model.MerchantFault;
        request.ShippingFeeApproved = model.MerchantFault && model.ApproveShippingFee && request.Items.Sum(x => x.ApprovedQuantity) == request.Order.Items.Sum(x => x.Quantity);
        request.ReviewerId = adminId;
        request.ReviewedAtUtc = _clock.GetUtcNow().UtcDateTime;
        if (decisionStatus == ReturnRequestStatus.Rejected)
        {
            request.Resolution = ReturnResolutionType.Reject;
            return await SaveTransitionAsync(request, adminId, decisionStatus, ReturnEventType.Rejected, model.Reason, cancellationToken);
        }

        var aggregateAmount = request.Items.Sum(x => x.ApprovedAmount)
            + (request.ShippingFeeApproved ? request.Order.ShippingFee : 0m);
        var succeededForOrder = (await _db.Refunds
            .Where(x => x.OrderId == request.OrderId && x.Status == RefundStatus.Succeeded)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken)).Sum();
        var remainingOrderAmount = Math.Max(0m, request.Order.Total - succeededForOrder);
        if (aggregateAmount > remainingOrderAmount)
            return ReturnResult.Fail("Số tiền hoàn vượt số tiền còn có thể hoàn của đơn hàng.");

        request.Resolution = aggregateAmount == remainingOrderAmount
            ? ReturnResolutionType.FullRefund
            : ReturnResolutionType.PartialRefund;
        var refundKey = $"return:{request.Id}:aggregate-refund";
        var refund = await _db.Refunds.SingleOrDefaultAsync(x => x.IdempotencyKey == refundKey, cancellationToken);
        if (refund == null)
        {
            refund = new Refund
            {
                ReturnRequestId = request.Id,
                ReturnRequestItemId = null,
                OrderId = request.OrderId,
                Amount = aggregateAmount,
                Method = RefundMethod.ManualBankTransfer,
                Status = RefundStatus.AwaitingDestination,
                IdempotencyKey = refundKey,
                CreatedByUserId = adminId,
                CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
            };
            _db.Refunds.Add(refund);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        request.Status = ReturnRequestStatus.ResolutionPending;
        var decisionType = decisionStatus == ReturnRequestStatus.Approved ? ReturnEventType.Approved : ReturnEventType.PartiallyApproved;
        _db.ReturnEvents.Add(NewEvent(decisionType, ReturnRequestStatus.UnderReview, decisionStatus, adminId, model.Reason, now, request.Id));
        _db.ReturnEvents.Add(NewEvent(ReturnEventType.RefundCreated, decisionStatus, ReturnRequestStatus.ResolutionPending, adminId, $"Tạo khoản hoàn {aggregateAmount:N0}₫.", now, request.Id));
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.ReturnStatusChanged,
            new { returnRequestId = request.Id, fromStatus = ReturnRequestStatus.UnderReview.ToString(), toStatus = decisionStatus.ToString(), actorUserId = adminId },
            $"return:{request.Id}:status:{decisionStatus}:{Guid.NewGuid():N}",
            cancellationToken);
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.RefundCreated,
            new { returnRequestId = request.Id, orderId = request.OrderId, amount = aggregateAmount, method = refund.Method.ToString(), refundIdempotencyKey = refundKey },
            $"refund:{refundKey}:created",
            cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return ReturnResult.Ok(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ReturnResult.Fail("Yêu cầu đã được nhân viên khác cập nhật. Vui lòng tải lại.", true);
        }
    }

    public async Task<ReturnResult> CancelAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var request = await _db.ReturnRequests.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (request == null) return ReturnResult.Fail("Không tìm thấy yêu cầu.");
        return await SaveTransitionAsync(request, userId, ReturnRequestStatus.Cancelled, ReturnEventType.Cancelled, "Khách hàng hủy yêu cầu.", cancellationToken);
    }

    public Task<ReturnResult> UpdateResolutionAsync(int id, int adminId, ReturnRequestStatus target, string note, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        var type = target switch
        {
            ReturnRequestStatus.ResolutionPending => ReturnEventType.ResolutionStarted,
            ReturnRequestStatus.ResolutionFailed => ReturnEventType.ResolutionFailed,
            ReturnRequestStatus.Resolved => ReturnEventType.Resolved,
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        return TransitionAsync(id, adminId, target, type, note, rowVersion, cancellationToken);
    }

    private async Task<ReturnResult> TransitionAsync(int id, int actorId, ReturnRequestStatus target, ReturnEventType type, string note, byte[] rowVersion, CancellationToken cancellationToken, Action<ReturnRequest>? update = null)
    {
        var request = await _db.ReturnRequests.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null) return ReturnResult.Fail("Không tìm thấy yêu cầu.");
        if (rowVersion.Length > 0) _db.Entry(request).Property(x => x.RowVersion).OriginalValue = rowVersion;
        update?.Invoke(request);
        return await SaveTransitionAsync(request, actorId, target, type, note, cancellationToken);
    }

    private async Task<ReturnResult> SaveTransitionAsync(ReturnRequest request, int actorId, ReturnRequestStatus target, ReturnEventType type, string? note, CancellationToken cancellationToken)
    {
        if (!Transitions.TryGetValue(request.Status, out var allowed) || !allowed.Contains(target)) return ReturnResult.Fail($"Không thể chuyển từ {request.Status} sang {target}.");
        var old = request.Status;
        request.Status = target;
        var now = _clock.GetUtcNow().UtcDateTime;
        if (target == ReturnRequestStatus.Resolved) request.ResolvedAtUtc = now;
        _db.ReturnEvents.Add(NewEvent(type, old, target, actorId, note, now, request.Id));
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.ReturnStatusChanged,
            new { returnRequestId = request.Id, fromStatus = old.ToString(), toStatus = target.ToString(), actorUserId = actorId },
            $"return:{request.Id}:status:{target}:{Guid.NewGuid():N}",
            cancellationToken);
        try { await _db.SaveChangesAsync(cancellationToken); return ReturnResult.Ok(request); }
        catch (DbUpdateConcurrencyException) { return ReturnResult.Fail("Yêu cầu đã được nhân viên khác cập nhật. Vui lòng tải lại.", true); }
    }

    private IQueryable<ReturnRequest> FullQuery() => _db.ReturnRequests.AsNoTracking().Include(x => x.Order).ThenInclude(x => x.Items).Include(x => x.User).Include(x => x.Items).ThenInclude(x => x.OrderItem).Include(x => x.Evidences).Include(x => x.Events).Include(x => x.Refunds);
    private static ReturnEvent NewEvent(ReturnEventType type, ReturnRequestStatus? from, ReturnRequestStatus? to, int actor, string? note, DateTime now, int requestId = 0) => new() { ReturnRequestId = requestId, Type = type, FromStatus = from, ToStatus = to, ActorUserId = actor, Note = note, CreatedAtUtc = now };
    private static bool IsSubmissionConcurrencyFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sql && sql.Number is 1205 or 3960) return true;
        return exception is DbUpdateConcurrencyException;
    }
    private static async Task RollbackSafelyAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction == null) return;
        try { await transaction.RollbackAsync(cancellationToken); }
        catch (InvalidOperationException) { }
    }
    private void ApplyVersion(ReturnRequest request, string encoded) { if (!string.IsNullOrWhiteSpace(encoded)) _db.Entry(request).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(encoded); }
}
