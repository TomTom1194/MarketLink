/* =====================================================================
   Update_07: Farmer_Category
   The categories a farmer ticks on the application form.
   A farmer can only post products in these categories.
   Safe to run more than once.
   ===================================================================== */
USE MarketDB;
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

-- Check
SELECT f.brand_name, c.category_name
FROM Farmer_Category fc
JOIN Farmer_Profile f ON f.farmer_id = fc.farmer_id
JOIN Product_Category c ON c.category_id = fc.category_id
ORDER BY f.brand_name, c.category_name;
