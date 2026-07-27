namespace Fruitables.Models.Returns;

public record RefundCalculationResult(decimal NetPaidAmount, decimal PreviouslyRefundedAmount, decimal RefundableAmount);
