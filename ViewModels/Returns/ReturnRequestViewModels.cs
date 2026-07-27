using System.ComponentModel.DataAnnotations;
using Fruitables.Models.Returns;
using Microsoft.AspNetCore.Http;

namespace Fruitables.ViewModels.Returns;

public class ReturnSubmitViewModel : IValidatableObject
{
    public int OrderId { get; set; }
    [Required, MaxLength(64)] public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(2000)] public string? CustomerNote { get; set; }
    public List<ReturnSubmitItemViewModel> Items { get; set; } = new();
    public List<IFormFile>? EvidenceFiles { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var item in Items.Where(x => x.Selected))
        {
            if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Trim().Length < 5)
                yield return new ValidationResult("Mô tả tối thiểu 5 ký tự là bắt buộc cho sản phẩm đã chọn.", [nameof(Items)]);
        }
    }
}

public class ReturnSubmitItemViewModel
{
    public bool Selected { get; set; }
    public int OrderItemId { get; set; }
    [Range(0, int.MaxValue)] public int Quantity { get; set; }
    public ReturnReasonCode Reason { get; set; }
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
}

public enum ReturnQueueBucket
{
    Intake,
    WaitingCustomer,
    Reviewing,
    Completed
}

public class ReturnQueueFilter
{
    public ReturnQueueBucket? Bucket { get; set; }
    public ReturnRequestStatus? Status { get; set; }
    public ReturnReasonCode? Reason { get; set; }
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
