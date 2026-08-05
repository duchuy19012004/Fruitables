using Microsoft.AspNetCore.Http;
using Fruitables.Models.Returns;
using Fruitables.ViewModels.Returns;

namespace Fruitables.Services.Returns;

public interface IReturnService
{
    Task<ReturnEligibilityViewModel> GetEligibilityAsync(int orderId, int userId);
    Task<ReturnOperationResult> CreateAsync(CreateReturnCommand command, int userId);
    Task<ReturnDetailViewModel?> GetCustomerDetailAsync(int returnRequestId, int userId);
    Task<ReturnOperationResult> CancelAsync(int returnRequestId, int userId);
    Task<ReturnOperationResult> AddCustomerInfoAsync(SupplementReturnCommand command, int userId);
    Task<ReturnOperationResult> RequestCustomerInfoAsync(RequestCustomerInfoCommand command, int adminId);
    Task<ReturnOperationResult> DecideAsync(DecideReturnCommand command, int adminId);
    Task<ReturnOperationResult> CompleteRefundAsync(CompleteRefundCommand command, int adminId);
}

public sealed record CreateReturnItemCommand(
    int OrderItemId,
    decimal RequestedQuantity,
    ReturnReasonCode Reason,
    string Description,
    IReadOnlyList<IFormFile> Evidence);

public sealed record CreateReturnCommand(
    int OrderId,
    IReadOnlyList<CreateReturnItemCommand> Items);

public sealed record SupplementReturnCommand(
    int ReturnRequestId,
    string Description,
    IReadOnlyList<IFormFile> Evidence,
    string RowVersion);

public sealed record RequestCustomerInfoCommand(
    int ReturnRequestId,
    string Note,
    string RowVersion);

public sealed record ReturnItemDecisionCommand(
    int OrderItemId,
    decimal ApprovedQuantity,
    bool Approved,
    string DecisionReason);

public sealed class DecideReturnCommand
{
    public int ReturnRequestId { get; init; }
    public IReadOnlyList<ReturnItemDecisionCommand> Items { get; init; } = [];
    public bool RefundShippingFee { get; init; }
    public string DecisionNote { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CompleteRefundCommand
{
    public int ReturnRequestId { get; init; }
    public bool Succeeded { get; init; }
    public string? TransactionReference { get; init; }
    public string? FailureReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ReturnOperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? ReturnRequestId { get; init; }
    public string? RowVersion { get; init; }
    public decimal ApprovedShippingFeeAmount { get; init; }
}
