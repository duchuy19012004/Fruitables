using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels.Returns;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class ReturnEligibilityService : IReturnEligibilityService
{
    private readonly ApplicationDbContext _db;
    private readonly IReturnPolicyService _policies;
    private readonly TimeProvider _clock;

    public ReturnEligibilityService(ApplicationDbContext db, IReturnPolicyService policies, TimeProvider clock)
    {
        _db = db;
        _policies = policies;
        _clock = clock;
    }

    public async Task<ReturnEligibilityResult> CheckOrderAsync(int orderId, int userId, ReturnReasonCode? reason = null, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
        if (order == null) return new(false, "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.", null, Array.Empty<ReturnItemEligibility>());
        if (order.Status != OrderStatus.Delivered) return new(false, "Chỉ đơn hàng đã giao mới có thể gửi yêu cầu hỗ trợ.", null, Array.Empty<ReturnItemEligibility>());
        if (order.DeliveredAtUtc == null) return new(false, "Chưa xác định được thời điểm giao hàng. Vui lòng liên hệ CSKH để được kiểm tra thủ công.", null, Array.Empty<ReturnItemEligibility>());

        var results = new List<ReturnItemEligibility>();
        foreach (var item in order.Items.OrderBy(x => x.Id))
        {
            if (reason.HasValue)
            {
                results.Add(await CheckLoadedItemAsync(order, item, reason.Value, cancellationToken));
                continue;
            }

            var reasonResults = new List<ReturnItemEligibility>();
            foreach (var candidate in Enum.GetValues<ReturnReasonCode>())
                reasonResults.Add(await CheckLoadedItemAsync(order, item, candidate, cancellationToken));
            results.Add(reasonResults.Where(x => x.Eligible).OrderBy(x => x.DeadlineAtUtc).FirstOrDefault()
                ?? reasonResults.First());
        }

        var eligible = results.Any(x => x.Eligible);
        return new(eligible, eligible ? null : results.Select(x => x.Error).FirstOrDefault(x => x != null), results.Where(x => x.DeadlineAtUtc != null).Select(x => x.DeadlineAtUtc).Min(), results);
    }

    public async Task<ReturnItemEligibility> CheckItemAsync(int orderId, int orderItemId, int userId, ReturnReasonCode reason, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
        if (order == null) return Ineligible(orderItemId, "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.");
        var item = order.Items.SingleOrDefault(x => x.Id == orderItemId);
        if (item == null) return Ineligible(orderItemId, "Sản phẩm không thuộc đơn hàng.");
        if (order.Status != OrderStatus.Delivered) return Ineligible(orderItemId, "Đơn hàng chưa được giao.");
        if (order.DeliveredAtUtc == null) return Ineligible(orderItemId, "Không xác định được thời điểm giao hàng.");
        return await CheckLoadedItemAsync(order, item, reason, cancellationToken);
    }

    private async Task<ReturnItemEligibility> CheckLoadedItemAsync(Order order, OrderItem item, ReturnReasonCode reason, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var policy = await _policies.ResolveAsync(item.ProductId, reason, now, cancellationToken);
        if (policy == null || !policy.IsEligible) return Ineligible(item.Id, "Lý do này không được chính sách hỗ trợ.", policy);
        var deadline = order.DeliveredAtUtc!.Value.AddHours(policy.ClaimWindowHours);
        if (now > deadline) return new(item.Id, false, "Đã hết thời hạn gửi yêu cầu.", 0, policy.EvidenceRequired, policy, deadline);

        var committed = await _db.ReturnRequestItems.AsNoTracking()
            .Where(x => x.OrderItemId == item.Id && x.ReturnRequest.Status != ReturnRequestStatus.Rejected && x.ReturnRequest.Status != ReturnRequestStatus.Cancelled && x.ReturnRequest.Status != ReturnRequestStatus.Expired)
            .SumAsync(x => x.ReturnRequest.Status == ReturnRequestStatus.Submitted || x.ReturnRequest.Status == ReturnRequestStatus.AwaitingEvidence || x.ReturnRequest.Status == ReturnRequestStatus.UnderReview ? x.RequestedQuantity : x.ApprovedQuantity, cancellationToken);
        var remaining = Math.Max(0, item.Quantity - committed);
        return remaining == 0
            ? new(item.Id, false, "Số lượng đã được yêu cầu hoặc giải quyết hết.", 0, policy.EvidenceRequired, policy, deadline)
            : new(item.Id, true, null, remaining, policy.EvidenceRequired, policy, deadline);
    }

    private static ReturnItemEligibility Ineligible(int itemId, string error, ReturnPolicy? policy = null) => new(itemId, false, error, 0, policy?.EvidenceRequired ?? false, policy, null);
}
