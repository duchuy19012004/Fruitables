using Fruitables.Models.Returns;

namespace Fruitables.Services.Interfaces;

public interface IRefundAmountCalculator
{
    Task<RefundCalculationResult> CalculateAsync(int orderItemId, int quantity, CancellationToken cancellationToken = default);
}
