using Fruitables.Models.Returns;
using Fruitables.ViewModels;

namespace Fruitables.ViewModels.Returns;

public sealed class ReturnEligibilityViewModel
{
    public int OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public bool CanCreate { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public DateTime? ClaimDeadlineAtUtc { get; init; }
    public int? ExistingRequestId { get; init; }
    public IReadOnlyList<ReturnEligibleItemViewModel> Items { get; init; } = [];
}

public sealed class ReturnEligibleItemViewModel
{
    public int OrderItemId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal OrderedQuantity { get; init; }
    public decimal MaxClaimableQuantity { get; init; }
}

public sealed class ReturnQueueFilter
{
    public ReturnRequestStatus? Status { get; init; }
    public string? Search { get; init; }
    public DateTime? FromDateUtc { get; init; }
    public DateTime? ToDateUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class ReturnQueueRowViewModel
{
    public int Id { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal RequestedAmount { get; init; }
    public ReturnRequestStatus Status { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
}

public sealed class ReturnDetailViewModel
{
    public int Id { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public int OrderId { get; init; }
    public int UserId { get; init; }
    public ReturnRequestStatus Status { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
    public DateTime ClaimDeadlineAtUtc { get; init; }
    public DateTime? SupplementDeadlineAtUtc { get; init; }
    public decimal RequestedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal ApprovedShippingFeeAmount { get; init; }
    public string? CustomerNote { get; init; }
    public string? AdminNote { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ReturnItemDetailViewModel> Items { get; init; } = [];
    public IReadOnlyList<ReturnEvidenceViewModel> Evidence { get; init; } = [];
    public IReadOnlyList<ReturnEventViewModel> Events { get; init; } = [];
    public RefundViewModel? Refund { get; init; }
}

public sealed class ReturnItemDetailViewModel
{
    public int Id { get; init; }
    public int OrderItemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public ReturnReasonCode Reason { get; init; }
    public ReturnItemDecisionStatus DecisionStatus { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal RequestedQuantity { get; init; }
    public decimal ApprovedQuantity { get; init; }
    public decimal RequestedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DecisionReason { get; init; }
    public IReadOnlyList<ReturnEvidenceViewModel> Evidence { get; init; } = [];
}

public sealed class ReturnEvidenceViewModel
{
    public int Id { get; init; }
    public string StorageKey { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime UploadedAtUtc { get; init; }
}

public sealed class ReturnEventViewModel
{
    public long Id { get; init; }
    public ReturnEventType EventType { get; init; }
    public ReturnRequestStatus? OldStatus { get; init; }
    public ReturnRequestStatus? NewStatus { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class RefundViewModel
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public decimal ShippingFeeAmount { get; init; }
    public RefundStatus Status { get; init; }
    public string? TransactionReference { get; init; }
    public string? FailureReason { get; init; }
}
