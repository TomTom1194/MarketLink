/* =====================================================================
   Brings an existing MarketDB up to date with the current code.
   Safe to run many times: each change runs only if it is still missing.
   (Covers Update_01 to Update_04.)
   ===================================================================== */

USE MarketDB;
GO

-- Update_01: drop coordinates, add google_url to Stalls
IF COL_LENGTH('Markets', 'latitude') IS NOT NULL
    ALTER TABLE Markets DROP COLUMN latitude, longitude;
IF COL_LENGTH('Stalls', 'latitude') IS NOT NULL
    ALTER TABLE Stalls DROP COLUMN latitude, longitude;
IF COL_LENGTH('Stalls', 'google_url') IS NULL
    ALTER TABLE Stalls ADD google_url NVARCHAR(500) NULL;
GO

-- Update_02: drop address from Markets
IF COL_LENGTH('Markets', 'address') IS NOT NULL
    ALTER TABLE Markets DROP COLUMN address;
GO

-- Update_03: market photo
IF COL_LENGTH('Markets', 'image_url') IS NULL
    ALTER TABLE Markets ADD image_url NVARCHAR(500) NULL;
GO

-- Update_04: farmer who suggested a market
IF COL_LENGTH('Markets', 'requested_by') IS NULL
    ALTER TABLE Markets ADD requested_by INT NULL REFERENCES Users(user_id);
GO

-- Update_05: list of product units for the farmer product form
IF OBJECT_ID(N'dbo.Product_Unit', N'U') IS NULL
    CREATE TABLE dbo.Product_Unit (
        unit        NVARCHAR(20) NOT NULL PRIMARY KEY,
        sort_order  INT          NOT NULL,
        is_active   BIT          NOT NULL DEFAULT 1
    );
GO

INSERT INTO dbo.Product_Unit (unit, sort_order)
SELECT source.unit, source.sort_order
FROM (VALUES
    (N'kg', 1), (N'g', 2), (N'liter', 3), (N'ml', 4),
    (N'bunch', 5), (N'piece', 6), (N'head', 7), (N'dozen', 8),
    (N'bag', 9), (N'box', 10), (N'tray', 11), (N'carton', 12)
) AS source(unit, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product_Unit AS existing WHERE existing.unit = source.unit);
GO

-- Update_07: categories each farmer is allowed to sell
IF OBJECT_ID(N'dbo.Farmer_Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Farmer_Category (
        farmer_id    INT NOT NULL REFERENCES Farmer_Profile(farmer_id),
        category_id  INT NOT NULL REFERENCES Product_Category(category_id),
        PRIMARY KEY (farmer_id, category_id)
    );

    -- Farmers that already exist: allow the categories of the products they already sell,
    -- or every active category if they have no product yet (so nobody is locked out).
    INSERT INTO dbo.Farmer_Category (farmer_id, category_id)
    SELECT DISTINCT p.farmer_id, p.category_id
    FROM Products p
    WHERE p.status <> 'removed';

    INSERT INTO dbo.Farmer_Category (farmer_id, category_id)
    SELECT f.farmer_id, c.category_id
    FROM Farmer_Profile f
    CROSS JOIN Product_Category c
    WHERE c.is_active = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.Farmer_Category fc WHERE fc.farmer_id = f.farmer_id);
END;
GO

-- Check: every column the code needs should be listed here
SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME = 'Markets' AND COLUMN_NAME IN ('map_url', 'image_url', 'requested_by'))
   OR (TABLE_NAME = 'Stalls'  AND COLUMN_NAME IN ('google_url'))
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT COUNT(*) AS product_unit_count FROM dbo.Product_Unit;
