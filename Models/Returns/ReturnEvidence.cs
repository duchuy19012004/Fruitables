using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnEvidence
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int? ReturnRequestItemId { get; set; }
    public int UploadedByUserId { get; set; }
    [Required, MaxLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, MaxLength(128)] public string StorageKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    [Required, MaxLength(64)] public string Sha256Checksum { get; set; } = string.Empty;
    public EvidenceScanStatus ScanStatus { get; set; } = EvidenceScanStatus.Pending;
    public bool IsInternal { get; set; }
    public DateTime UploadedAtUtc { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public ReturnRequestItem? ReturnRequestItem { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public ICollection<ReturnEvidenceLink> ItemLinks { get; set; } = new List<ReturnEvidenceLink>();
}
