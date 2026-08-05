using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnEvidence
{
    public int Id { get; set; }

    public int ReturnRequestId { get; set; }
    public int? ReturnRequestItemId { get; set; }

    [Required, MaxLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ReturnRequest ReturnRequest { get; set; } = null!;
    public virtual ReturnRequestItem? ReturnRequestItem { get; set; }
    public virtual User UploadedByUser { get; set; } = null!;
}
