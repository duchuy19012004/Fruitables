using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class RefundAmountCalculator : IRefundAmountCalculator
{
    private readonly ApplicationDbContext _db;
    public RefundAmountCalculator(ApplicationDbContext db) => _db = db;

    public async Task<RefundCalculationResult> CalculateAsync(int orderItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.OrderItems.AsNoTracking().Include(x => x.Order).SingleOrDefaultAsync(x => x.Id == orderItemId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy sản phẩm trong đơn hàng.");
        if (quantity <= 0 || quantity > item.Quantity) throw new ArgumentOutOfRangeException(nameof(quantity));

        var lines = await _db.OrderItems.AsNoTracking().Where(x => x.OrderId == item.OrderId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var allocations = AllocateDiscount(lines.Select(x => (x.Id, x.Total)).ToList(), item.Order.Discount);
        var lineNet = Math.Max(0, item.Total - allocations[item.Id]);
        var requestedNet = quantity == item.Quantity ? lineNet : decimal.Round(lineNet * quantity / item.Quantity, 2, MidpointRounding.AwayFromZero);
        var priorItem = (await _db.Refunds.AsNoTracking().Where(x => x.ReturnRequestItem != null && x.ReturnRequestItem.OrderItemId == item.Id && x.Status == RefundStatus.Succeeded).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum();
        var priorOrder = (await _db.Refunds.AsNoTracking().Where(x => x.OrderId == item.OrderId && x.Status == RefundStatus.Succeeded).Select(x => x.Amount).ToListAsync(cancellationToken)).Sum();
        var paidCap = item.Order.PaymentStatus == Models.PaymentStatus.Pending ? 0 : item.Order.Total;
        var refundable = Math.Max(0, Math.Min(requestedNet - priorItem, paidCap - priorOrder));
        return new(lineNet, priorItem, refundable);
    }

    internal static Dictionary<int, decimal> AllocateDiscount(IReadOnlyList<(int Id, decimal Total)> lines, decimal discount)
    {
        var result = lines.ToDictionary(x => x.Id, _ => 0m);
        var basis = lines.Sum(x => Math.Max(0, x.Total));
        var target = Math.Min(Math.Max(0, discount), basis);
        if (basis == 0 || target == 0) return result;

        foreach (var line in lines)
            result[line.Id] = decimal.Floor(target * Math.Max(0, line.Total) / basis * 100m) / 100m;
        var cents = (int)decimal.Round((target - result.Values.Sum()) * 100m, 0, MidpointRounding.AwayFromZero);
        foreach (var line in lines.OrderBy(x => x.Id).Take(cents)) result[line.Id] += 0.01m;
        return result;
    }
}
