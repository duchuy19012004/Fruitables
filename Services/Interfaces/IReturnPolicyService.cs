using Fruitables.Models.Returns;

namespace Fruitables.Services.Interfaces;

public interface IReturnPolicyService
{
    Task<ReturnPolicy?> ResolveAsync(int productId, ReturnReasonCode reason, DateTime utcNow, CancellationToken cancellationToken = default);
}
