/*
Controlled backfill for DeliveredAtUtc.
1. Run ReturnDeliveredAtBackfillReport.sql.
2. Verify each timestamp and convert it to UTC outside this script.
3. Add only reviewed rows below, execute inside a transaction, inspect, then COMMIT.
Never derive UTC by adding/subtracting seven hours from every legacy row.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Verified TABLE
(
    OrderId int NOT NULL PRIMARY KEY,
    DeliveredAtUtc datetime2 NOT NULL,
    ReviewedBy nvarchar(200) NOT NULL,
    ReviewNote nvarchar(1000) NOT NULL
);

/* Reviewed rows are intentionally explicit. Example:
INSERT INTO @Verified(OrderId, DeliveredAtUtc, ReviewedBy, ReviewNote)
VALUES (123, '2026-07-20T03:15:00Z', N'Nguyễn Văn A', N'Đối chiếu biên bản giao hàng GHN');
*/

IF EXISTS
(
    SELECT 1 FROM @Verified v
    LEFT JOIN Orders o ON o.Id = v.OrderId
    WHERE o.Id IS NULL OR o.Status <> 3 OR o.DeliveredAtUtc IS NOT NULL
)
    THROW 51000, 'Danh sách xác minh chứa đơn không hợp lệ hoặc đã được backfill.', 1;

UPDATE o
SET DeliveredAtUtc = v.DeliveredAtUtc
FROM Orders o
INNER JOIN @Verified v ON v.OrderId = o.Id
WHERE o.Status = 3 AND o.DeliveredAtUtc IS NULL;

SELECT o.Id, o.OrderNumber, o.DeliveredAtUtc, v.ReviewedBy, v.ReviewNote
FROM Orders o
INNER JOIN @Verified v ON v.OrderId = o.Id;

SELECT o.Id, o.OrderNumber, o.CreatedAt, MIN(h.CreatedAt) AS FirstDeliveredHistoryAt
FROM Orders o
LEFT JOIN OrderStatusHistories h ON h.OrderId = o.Id AND h.NewStatus = 3
WHERE o.Status = 3 AND o.DeliveredAtUtc IS NULL
GROUP BY o.Id, o.OrderNumber, o.CreatedAt
ORDER BY o.Id;

/* Change to COMMIT only after reviewing both result sets. */
ROLLBACK TRANSACTION;
