/* =====================================================================
   Update_05: Product_Unit table
   The list of units a farmer can choose on the "Add product" form.
   Safe to run more than once.
   ===================================================================== */
USE MarketDB;
GO

IF OBJECT_ID(N'dbo.Product_Unit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Product_Unit (
        unit        NVARCHAR(20) NOT NULL PRIMARY KEY,   -- e.g. kg, bunch, box
        sort_order  INT          NOT NULL,               -- order in the dropdown
        is_active   BIT          NOT NULL DEFAULT 1      -- 0 = hidden from the dropdown
    );
END;
GO

-- Add the default units (skips units that already exist)
INSERT INTO dbo.Product_Unit (unit, sort_order)
SELECT source.unit, source.sort_order
FROM (VALUES
    (N'kg', 1), (N'g', 2), (N'liter', 3), (N'ml', 4),
    (N'bunch', 5), (N'piece', 6), (N'head', 7), (N'dozen', 8),
    (N'bag', 9), (N'box', 10), (N'tray', 11), (N'carton', 12)
) AS source(unit, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product_Unit AS existing WHERE existing.unit = source.unit);
GO

-- Check
SELECT unit, sort_order, is_active FROM dbo.Product_Unit ORDER BY sort_order;
