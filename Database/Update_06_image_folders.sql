/* =====================================================================
   Update_06: uploaded photos moved from wwwroot/uploads/... to wwwroot/images/...
   Points the saved image links to the new folders. Safe to run more than once.
   ===================================================================== */
USE MarketDB;
GO

UPDATE Markets  SET image_url = REPLACE(image_url, '/uploads/markets/',  '/images/markets/')
WHERE image_url LIKE '/uploads/markets/%';

UPDATE Products SET image_url = REPLACE(image_url, '/uploads/products/', '/images/products/')
WHERE image_url LIKE '/uploads/products/%';

UPDATE Order_Snapshot SET image_url = REPLACE(image_url, '/uploads/products/', '/images/products/')
WHERE image_url LIKE '/uploads/products/%';
GO

-- Check: should return no rows
SELECT 'Markets' AS table_name, market_id AS id, image_url FROM Markets WHERE image_url LIKE '/uploads/%'
UNION ALL
SELECT 'Products', product_id, image_url FROM Products WHERE image_url LIKE '/uploads/%';
