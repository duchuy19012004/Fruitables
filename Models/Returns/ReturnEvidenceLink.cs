namespace Fruitables.Models.Returns;

public class ReturnEvidenceLink
{
    public int ReturnEvidenceId { get; set; }
    public int ReturnRequestItemId { get; set; }

    public ReturnEvidence Evidence { get; set; } = null!;
    public ReturnRequestItem ReturnRequestItem { get; set; } = null!;
}
