namespace Fruitables.Services.Outbox;

public static class OutboxMessageTypes
{
    public const string ReturnSubmitted = "returns.request.submitted";
    public const string ReturnStatusChanged = "returns.request.status-changed";
    public const string RefundCreated = "returns.refund.created";
    public const string RefundSucceeded = "returns.refund.succeeded";
}
