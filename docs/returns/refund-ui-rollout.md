# Refund UI rollout

## Before migration

1. Back up the target database.
2. Confirm the Data Protection key ring is persistent and access-restricted.
3. Run the nonterminal return report below.
4. Assign operational users to CustomerSupport or Finance RBAC roles. Each user must also retain the legacy Admin or SuperAdmin role required by the admin area.

## Nonterminal return report

Status values: Rejected=5, Resolved=8, Cancelled=9, Expired=10.

```sql
SELECT
    rr.Id,
    rr.ReturnNumber,
    rr.Status,
    COUNT(r.Id) AS RefundCount,
    SUM(CASE WHEN r.ReturnRequestItemId IS NOT NULL THEN 1 ELSE 0 END) AS ItemRefundCount,
    SUM(CASE WHEN r.ReturnRequestItemId IS NULL THEN 1 ELSE 0 END) AS AggregateRefundCount
FROM ReturnRequests rr
LEFT JOIN Refunds r ON r.ReturnRequestId = rr.Id
WHERE rr.Status NOT IN (5, 8, 9, 10)
GROUP BY rr.Id, rr.ReturnNumber, rr.Status
ORDER BY rr.Id;
```

Do not enable the new UI while a nonterminal request has an item-level refund. Resolve it through the old flow first. For an approved request with no refund, create the aggregate refund with the same rules and stable idempotency key used by `ReturnService.DecideAsync`, then review the resulting amount before enabling the UI.

## After migration

1. Verify CustomerSupport has returns.view, returns.review, returns.approve, and returns.reject.
2. Verify Finance has returns.refund.
3. Verify generic Admin no longer has returns.refund.
4. Verify SuperAdmin retains all return permissions.
5. Run the three-role smoke flow.
6. Keep evidence upload closed to public production until malware scanning or quarantine exists.

## Rollback

Disable the new UI before rolling back. Do not roll back while any refund contains destination data or uses AwaitingDestination, AwaitingApproval, or Processing. Finish or cancel those refunds first, export the audit report, then rehearse the Down migration on a restored test backup.
