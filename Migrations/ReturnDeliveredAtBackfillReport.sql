/*
Read-only report. Historical OrderStatusHistory.CreatedAt contains mixed UTC and UTC+7
values, so this script does not update Orders automatically.
*/
SELECT
    o.Id AS OrderId,
    o.OrderNumber,
    o.CreatedAt AS OrderCreatedAtLegacy,
    MIN(h.CreatedAt) AS FirstDeliveredHistoryAt,
    CASE
        WHEN MIN(h.CreatedAt) IS NULL THEN 'NO_DELIVERED_HISTORY'
        WHEN MIN(h.CreatedAt) > SYSUTCDATETIME() THEN 'FUTURE_TIMESTAMP_REVIEW'
        WHEN MIN(h.CreatedAt) < DATEADD(day, -90, o.CreatedAt) THEN 'BEFORE_ORDER_REVIEW'
        ELSE 'TIMEZONE_SOURCE_REVIEW'
    END AS BackfillDecision
FROM Orders o
LEFT JOIN OrderStatusHistories h ON h.OrderId = o.Id AND h.NewStatus = 3
WHERE o.Status = 3 AND o.DeliveredAtUtc IS NULL
GROUP BY o.Id, o.OrderNumber, o.CreatedAt
ORDER BY FirstDeliveredHistoryAt, o.Id;

/*
After an operator verifies the source timezone, update individual rows explicitly:
UPDATE Orders SET DeliveredAtUtc = @VerifiedUtc WHERE Id = @OrderId AND DeliveredAtUtc IS NULL;
*/
