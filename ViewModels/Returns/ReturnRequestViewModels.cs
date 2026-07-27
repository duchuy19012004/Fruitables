using System.ComponentModel.DataAnnotations;
using Fruitables.Models.Returns;
using Microsoft.AspNetCore.Http;

namespace Fruitables.ViewModels.Returns;

public class ReturnSubmitViewModel
{
    public int OrderId { get; set; }
    [Required, MaxLength(64)] public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(2000)] public string? CustomerNote { get; set; }
    public List<ReturnSubmitItemViewModel> Items { get; set; } = new();
    public List<IFormFile>? EvidenceFiles { get; set; }
}

public class ReturnSubmitItemViewModel
{
    public bool Selected { get; set; }
    public int OrderItemId { get; set; }
    [Range(0, int.MaxValue)] public int Quantity { get; set; }
    public ReturnReasonCode Reason { get; set; }
    public ReturnResolutionType RequestedResolution { get; set; }
    [StringLength(1000)] public string Description { get; set; } = string.Empty;
}

public class ReturnDecisionViewModel
{
    public int ReturnRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Reason { get; set; }
    public bool MerchantFault { get; set; }
    public bool ApproveShippingFee { get; set; }
    public List<ReturnDecisionItemViewModel> Items { get; set; } = new();
}

public class ReturnDecisionItemViewModel
{
    public int ReturnRequestItemId { get; set; }
    [Range(0, int.MaxValue)] public int ApprovedQuantity { get; set; }
    public ReturnResolutionType Resolution { get; set; }
}

public class ReturnQueueFilter
{
    public ReturnRequestStatus? Status { get; set; }
    public ReturnReasonCode? Reason { get; set; }
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
