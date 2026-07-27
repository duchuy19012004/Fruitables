using Fruitables.Models.Returns;

namespace Fruitables.Services.Interfaces;

public interface IRefundService
{
    Task<(bool Success, string? Error, Refund? Refund)> CreateAsync(int returnRequestId, int returnRequestItemId, decimal amount, RefundMethod method, string idempotencyKey, int adminId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ConfirmManualAsync(int refundId, string transactionReference, string transferEvidenceStorageKey, int financeUserId, CancellationToken cancellationToken = default);
}
