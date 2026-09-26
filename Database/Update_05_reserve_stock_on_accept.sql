/* Run once when deploying the order flow that reserves stock on farmer acceptance.
   Safe to rerun. Rebuilds reserved quantities from accepted order snapshots.
   Run while order writes are paused so old and new application versions do not overlap. */
USE [MarketDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

;WITH ReservedTotals AS
(
    SELECT stock.stock_price_id,
           COALESCE(SUM(CASE WHEN orders.status = 'accepted' THEN snapshot.quantity ELSE 0 END), 0) AS expected_reserved
    FROM Stock_Price AS stock
    LEFT JOIN Order_Snapshot AS snapshot ON snapshot.stock_price_id = stock.stock_price_id
    LEFT JOIN Orders AS orders ON orders.order_id = snapshot.order_id
    GROUP BY stock.stock_price_id
)
UPDATE stock
SET stock.quantity_reserved = totals.expected_reserved
FROM Stock_Price AS stock
JOIN ReservedTotals AS totals ON totals.stock_price_id = stock.stock_price_id
WHERE stock.quantity_reserved <> totals.expected_reserved;

SELECT @@ROWCOUNT AS corrected_stock_rows;
COMMIT TRANSACTION;
