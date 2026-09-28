/* Translate the Vietnamese unit values used by the farmer portal.
   Products and order snapshots retain their numeric quantities and prices.
   Old Product_Unit rows are kept inactive so this is safe to run again.
   Run PRODUCT_UNIT_SEED.sql first to add the English choices. */
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
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Product_Unit', N'U') IS NULL
    THROW 51011, 'Run PRODUCT_UNIT_SEED.sql before this script.', 1;

DECLARE @Translations TABLE (old_unit NVARCHAR(20) PRIMARY KEY, new_unit NVARCHAR(20) NOT NULL);
INSERT INTO @Translations (old_unit, new_unit) VALUES
    (N'lạng', N'100 g'), (N'tạ', N'100 kg'), (N'tấn', N'tonne'),
    (N'lít', N'liter'), (N'bó', N'bunch'), (N'mớ', N'bundle'),
    (N'củ', N'root'), (N'quả', N'fruit'), (N'trái', N'fruit'),
    (N'chiếc', N'piece'), (N'cái', N'piece'),
    (N'túi', N'bag'), (N'hộp', N'box'), (N'thùng', N'carton');

UPDATE product SET unit = translation.new_unit
FROM dbo.Products AS product
JOIN @Translations AS translation ON translation.old_unit = product.unit;

UPDATE snapshot SET unit = translation.new_unit
FROM dbo.Order_Snapshot AS snapshot
JOIN @Translations AS translation ON translation.old_unit = snapshot.unit;

UPDATE dbo.Product_Unit SET is_active = 0
WHERE unit IN (SELECT old_unit FROM @Translations);

UPDATE dbo.Product_Unit SET is_active = 1
WHERE unit IN (SELECT DISTINCT new_unit FROM @Translations);

UPDATE dbo.Product_Category SET category_name = N'Vegetables'
WHERE slug = N'vegetables' AND category_name <> N'Vegetables';
UPDATE dbo.Product_Category SET category_name = N'Fruits'
WHERE slug = N'fruits' AND category_name <> N'Fruits';
UPDATE dbo.Product_Exp SET exp_name = N'Short term', duration_days = 1, description = N'Visible for 24 hours'
WHERE exp_code = N'SHORT';
UPDATE dbo.Product_Exp SET exp_name = N'Long term', duration_days = 7, description = N'Visible for 7 days'
WHERE exp_code = N'LONG';

COMMIT TRANSACTION;

SELECT unit, is_active FROM dbo.Product_Unit ORDER BY sort_order, unit;
