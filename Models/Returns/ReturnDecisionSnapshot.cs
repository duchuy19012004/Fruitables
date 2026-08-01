namespace Fruitables.Models.Returns;

public sealed record ReturnDecisionSnapshot(
    int OrderItemId,
    int ApprovedQuantity,
    int ApprovedDamagePercentage,
    ReturnCauseCode Cause,
    decimal ApprovedAmount);
