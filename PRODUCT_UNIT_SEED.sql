USE [MarketDB];
GO

-- English unit choices for the farmer product form. Existing products are unchanged.
IF OBJECT_ID(N'dbo.Product_Unit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Product_Unit
    (
        unit NVARCHAR(20) NOT NULL CONSTRAINT PK_Product_Unit PRIMARY KEY,
        sort_order INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_Product_Unit_IsActive DEFAULT (1)
    );
END;
GO

WITH Units(unit, sort_order) AS
(
    SELECT unit, sort_order FROM (VALUES
        (N'kg', 1), (N'g', 2), (N'100 g', 3), (N'100 kg', 4),
        (N'tonne', 5), (N'liter', 6), (N'ml', 7), (N'bunch', 8),
        (N'bundle', 9), (N'root', 10), (N'fruit', 11),
        (N'piece', 12), (N'bag', 13), (N'box', 14), (N'carton', 15)
    ) AS source(unit, sort_order)
)
INSERT INTO dbo.Product_Unit(unit, sort_order)
SELECT source.unit, source.sort_order
FROM Units AS source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product_Unit AS existing WHERE existing.unit = source.unit);
GO

SELECT unit, sort_order, is_active FROM dbo.Product_Unit ORDER BY sort_order;
