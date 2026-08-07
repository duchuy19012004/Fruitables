namespace Fruitables.Models.Returns;

public enum ReturnRequestStatus
{
    Submitted,
    UnderReview,
    AwaitingCustomerInfo,
    AwaitingRefund,
    Refunded,
    Rejected,
    Cancelled
}

public enum ReturnItemDecisionStatus
{
    Pending,
    Approved,
    Rejected
}

public enum RefundStatus
{
    Pending,
    Succeeded,
    Failed
}

public enum ReturnReasonCode
{
    Damaged,
    Mold,
    NotFresh,
    WrongItem,
    MissingItem
}

public enum ReturnEventType
{
    Submitted,
    CustomerInfoRequested,
    CustomerInfoAdded,
    Approved,
    Rejected,
    Cancelled,
    RefundCreated,
    RefundCompleted,
    RefundFailed
}
