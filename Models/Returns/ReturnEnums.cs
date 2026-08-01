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

public enum ReturnItemDecisionStatus
{
    Submitted,
    AwaitingCustomerInfo,
    UnderReview,
    DecisionProposed,
    AwaitingManagerApproval,
    Approved,
    RejectedPendingAppeal,
    Rejected,
    Expired,
    Cancelled
}

public enum ReturnDamagePercentage
{
    None = 0,
    TwentyFive = 25,
    Fifty = 50,
    SeventyFive = 75,
    Full = 100
}

[Flags]
public enum ReturnDamagePercentageOptions
{
    None = 0,
    TwentyFive = 1,
    Fifty = 2,
    SeventyFive = 4,
    Full = 8,
    All = TwentyFive | Fifty | SeventyFive | Full
}

public enum ReturnCauseCode
{
    Unknown,
    MerchantPacking,
    MerchantQuality,
    CarrierDelay,
    CarrierDamage,
    CustomerStorage,
    CustomerUse
}

public enum ReturnCostBearer
{
    None,
    Merchant,
    Carrier,
    Customer,
    Shared,
    Unknown
}

public enum ReturnApprovalAction
{
    Review,
    ManagerApproval,
    FinanceProcess,
    FinanceConfirm,
    RiskManagement,
    PolicyManagement
}

public enum ReturnAccountSupportLevel
{
    None,
    Reminder,
    IncreasedVerification,
    RestrictedFastSupport,
    SelfServiceSuspended
}

public enum RefundFailureKind
{
    None,
    Retryable,
    Terminal
}

public enum ReturnDecisionProposalStatus
{
    Draft,
    AwaitingManagerApproval,
    Approved,
    Returned
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
public enum InventoryDispositionType
{
    NotReturned,
    Quarantined,
    Discarded,
    Donated,
    ReturnedToSupplier,
    Restocked,
    DisposedByCustomer,
    CustomerKeptWrongItem,
    NoPhysicalItem
}
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
    RefundDestinationCorrectionRequested,
    DecisionProposed,
    ManagerApprovalRequested,
    ManagerApproved,
    ManagerReturned,
    AppealSubmitted,
    SupportLevelChanged,
    PolicyChanged,
    RefundFallbackRequested
}
