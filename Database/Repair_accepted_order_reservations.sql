/* Only for databases containing accepted orders from older code that did not reserve stock.
   Stop the web app while this script runs. It is safe to rerun. */
USE MarketDB;
GO
SET XACT_ABORT ON;

BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM Stock_Price AS stock
        OUTER APPLY (
            SELECT COALESCE(SUM(item.quantity), 0) AS needed
            FROM Order_Snapshot AS item
            JOIN Orders AS orders ON orders.order_id = item.order_id
            WHERE item.stock_price_id = stock.stock_price_id AND orders.status = 'accepted'
        ) AS accepted
        WHERE accepted.needed > stock.quantity_in - stock.quantity_sold
    )
        THROW 51005, 'Accepted orders exceed available stock. Review stock before repairing reservations.', 1;

    UPDATE stock
    SET stock.quantity_reserved = accepted.needed
    FROM Stock_Price AS stock
    OUTER APPLY (
        SELECT COALESCE(SUM(item.quantity), 0) AS needed
        FROM Order_Snapshot AS item
        JOIN Orders AS orders ON orders.order_id = item.order_id
        WHERE item.stock_price_id = stock.stock_price_id AND orders.status = 'accepted'
    ) AS accepted;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
