using Fruitables.Models.Returns;
using Fruitables.ViewModels.Returns;

namespace Fruitables.Services.Interfaces;

public interface IReturnEligibilityService
{
    Task<ReturnEligibilityResult> CheckOrderAsync(int orderId, int userId, ReturnReasonCode? reason = null, CancellationToken cancellationToken = default);
    Task<ReturnItemEligibility> CheckItemAsync(int orderId, int orderItemId, int userId, ReturnReasonCode reason, CancellationToken cancellationToken = default);
}
