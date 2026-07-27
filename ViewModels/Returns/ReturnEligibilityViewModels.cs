using Fruitables.Models.Returns;

namespace Fruitables.ViewModels.Returns;

public record ReturnEligibilityResult(bool Eligible, string? Error, DateTime? DeadlineAtUtc, IReadOnlyList<ReturnItemEligibility> Items);
public record ReturnItemEligibility(int OrderItemId, bool Eligible, string? Error, int RemainingQuantity, bool EvidenceRequired, ReturnPolicy? Policy, DateTime? DeadlineAtUtc);
