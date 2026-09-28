/* Minimal choices for the farmer product form on an existing MarketDB.
   Safe to run again. PRODUCT_UNIT_SEED.sql supplies the unit choices. */
USE MarketDB;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Product_Category WHERE slug = N'vegetables')
    INSERT INTO dbo.Product_Category (category_name, slug, is_active)
    VALUES (N'Vegetables', N'vegetables', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Product_Category WHERE slug = N'fruits')
    INSERT INTO dbo.Product_Category (category_name, slug, is_active)
    VALUES (N'Fruits', N'fruits', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Product_Exp WHERE exp_code = N'SHORT')
    INSERT INTO dbo.Product_Exp (exp_code, exp_name, duration_days, description)
    VALUES (N'SHORT', N'Short term', 1, N'Visible for 24 hours');

IF NOT EXISTS (SELECT 1 FROM dbo.Product_Exp WHERE exp_code = N'LONG')
    INSERT INTO dbo.Product_Exp (exp_code, exp_name, duration_days, description)
    VALUES (N'LONG', N'Long term', 7, N'Visible for 7 days');

COMMIT TRANSACTION;

SELECT category_id, category_name, is_active FROM dbo.Product_Category ORDER BY category_id;
SELECT exp_id, exp_code, duration_days FROM dbo.Product_Exp ORDER BY duration_days;
