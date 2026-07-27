namespace Fruitables.Models.Returns;

public enum ReturnRequestStatus
{
    Submitted,
    AwaitingEvidence,
    UnderReview,
    Approved,
    PartiallyApproved,
    Rejected,
    ResolutionPending,
    ResolutionFailed,
    Resolved,
    Cancelled,
    Expired
}

public enum ReturnReasonCode
{
    DamagedOrBruised,
    SpoiledOrMoldy,
    TemperatureIssue,
    WiltedOrNotFresh,
    WrongItem,
    MissingItem,
    UnderweightOrShortQuantity,
    LateDeliveryCausedDamage,
    FoodSafetyConcern,
    ChangeOfMind,
    Other
}

public enum ReturnResolutionType { None, PartialRefund, FullRefund, Replacement, StoreCredit, Reject }
public enum RefundStatus { Pending, AwaitingDestination, AwaitingApproval, Processing, Succeeded, Failed, Cancelled }
public enum RefundMethod { ManualBankTransfer, OriginalPaymentMethod, StoreCredit }
public enum InventoryDispositionType { NotReturned, Quarantined, Discarded, Donated, ReturnedToSupplier, Restocked }
public enum EvidenceScanStatus { Pending, Clean, Rejected, ScanFailed }
public enum ReturnPolicyScope { Default, Category, Product }
public enum ReturnEventType
{
    Submitted,
    EvidenceAdded,
    EvidenceRequested,
    ReviewStarted,
    Approved,
    PartiallyApproved,
    Rejected,
    Cancelled,
    Expired,
    ResolutionStarted,
    ResolutionFailed,
    Resolved,
    RefundCreated,
    RefundSucceeded,
    RefundFailed,
    DispositionRecorded,
    RefundDestinationSubmitted,
    RefundDestinationViewed,
    RefundProcessingStarted,
    RefundDestinationCorrectionRequested
}
