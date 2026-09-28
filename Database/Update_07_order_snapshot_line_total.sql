/* Existing databases may have line_total as a required ordinary column.
   The OrderSnapshot model expects SQL Server to calculate it.
   A fresh database created from MarketLink.sql already has the correct column.
   Run only on the database used by the web app; re-running is safe. */
USE MarketDB;
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.Order_Snapshot', N'U') IS NULL
    THROW 51010, 'Order_Snapshot table is missing.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Order_Snapshot', N'line_total') IS NULL
BEGIN
    ALTER TABLE dbo.Order_Snapshot
        ADD line_total AS CAST(unit_price * quantity AS DECIMAL(12,2)) PERSISTED;
END
ELSE IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Order_Snapshot'), N'line_total', 'IsComputed') = 0
BEGIN
    ALTER TABLE dbo.Order_Snapshot DROP COLUMN line_total;
    ALTER TABLE dbo.Order_Snapshot
        ADD line_total AS CAST(unit_price * quantity AS DECIMAL(12,2)) PERSISTED;
END;

COMMIT TRANSACTION;

SELECT c.name, c.is_computed, cc.definition
FROM sys.columns AS c
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id AND cc.column_id = c.column_id
WHERE c.object_id = OBJECT_ID(N'dbo.Order_Snapshot') AND c.name = N'line_total';
