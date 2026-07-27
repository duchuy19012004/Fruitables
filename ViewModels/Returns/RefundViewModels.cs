using System.ComponentModel.DataAnnotations;
using Fruitables.Models.Returns;

namespace Fruitables.ViewModels.Returns;

public sealed class RefundDestinationInputViewModel
{
    public int RefundId { get; set; }
    public int ReturnRequestId { get; set; }

    [Required, StringLength(50, MinimumLength = 2)]
    public string BankCode { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9A-Za-z]{6,34}$")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string AccountHolder { get; set; } = string.Empty;
}

public enum RefundQueueBucket
{
    WaitingCustomer,
    Ready,
    Working,
    Completed
}

public sealed class RefundQueueFilter
{
    public RefundQueueBucket Bucket { get; set; } = RefundQueueBucket.Ready;
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class RefundQueueItemViewModel
{
    public int RefundId { get; init; }
    public int ReturnRequestId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public RefundStatus Status { get; init; }
    public string? BankCode { get; init; }
    public string? AccountLast4 { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class FinanceRefundViewModel
{
    public int RefundId { get; init; }
    public int ReturnRequestId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public RefundStatus Status { get; init; }
    public string? BankCode { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountHolder { get; init; }
    public string? AccountLast4 { get; init; }
}

public sealed class RefundFailureInputViewModel
{
    public int RefundId { get; set; }
    public bool RequestCustomerCorrection { get; set; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}
