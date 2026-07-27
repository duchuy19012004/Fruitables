using Fruitables.ViewModels.Returns;

namespace Fruitables.Services.Interfaces;

public interface IRefundService
{
    Task<(bool Success, string? Error)> SaveDestinationAsync(
        int refundId,
        int customerId,
        RefundDestinationInputViewModel model,
        CancellationToken cancellationToken = default);

    Task<List<RefundQueueItemViewModel>> GetQueueAsync(
        RefundQueueFilter filter,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, FinanceRefundViewModel? Data)> GetFinanceTaskAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> StartProcessingAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> FailAsync(
        int refundId,
        int financeUserId,
        RefundFailureInputViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> ConfirmManualAsync(
        int refundId,
        string transactionReference,
        string transferEvidenceStorageKey,
        int financeUserId,
        CancellationToken cancellationToken = default);
}
