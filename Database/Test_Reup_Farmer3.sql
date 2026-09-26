/* =====================================================================
   Test data: 2 NEW products for farmer 3
     - "Test Expired Bok Choy" : listing period is over  -> hidden, needs re-up
     - "Test Sold Out Bananas" : all stock sold          -> needs re-up
   Used to test the farmer alerts (bell + Notifications page) and the Re-up page.
   Safe to re-run: products with the same name are not added twice.
   ===================================================================== */
USE MarketDB;
GO

DECLARE @farmerId INT = 3;
DECLARE @now DATETIME2 = SYSDATETIME();

DECLARE @stallId    INT = (SELECT TOP 1 stall_id    FROM Stalls            WHERE farmer_id = @farmerId AND is_active = 1 ORDER BY stall_id);
DECLARE @categoryId INT = (SELECT TOP 1 category_id FROM Product_Category  WHERE is_active = 1 ORDER BY category_id);
DECLARE @shortExpId INT = (SELECT exp_id FROM Product_Exp WHERE exp_code = 'SHORT');
DECLARE @longExpId  INT = (SELECT exp_id FROM Product_Exp WHERE exp_code = 'LONG');

IF @stallId IS NULL OR @categoryId IS NULL OR @shortExpId IS NULL OR @longExpId IS NULL
BEGIN
    SELECT @stallId AS stall_id, @categoryId AS category_id, @shortExpId AS short_exp_id, @longExpId AS long_exp_id;
    THROW 50000, 'Missing data: farmer 3 needs an active stall, and there must be an active category and SHORT / LONG periods.', 1;
END;

/* ---------- 1) Expired product ---------- */
IF NOT EXISTS (SELECT 1 FROM Products WHERE farmer_id = @farmerId AND product_name = N'Test Expired Bok Choy')
BEGIN
    INSERT INTO Products (farmer_id, category_id, exp_id, product_name, description, unit, image_url,
                          published_at, expires_at, status, created_at)
    VALUES (@farmerId, @categoryId, @shortExpId, N'Test Expired Bok Choy', N'Test data: the 24-hour listing ended an hour ago.',
            N'kg', N'/images/products/bok-choy.jpg',
            DATEADD(HOUR, -25, @now),      -- published 25 hours ago
            DATEADD(HOUR, -1, @now),       -- SHORT listing ended 1 hour ago
            'active', DATEADD(HOUR, -25, @now));

    INSERT INTO Stock_Price (product_id, stall_id, price, quantity_in, quantity_reserved, quantity_sold,
                             change_type, effective_from, effective_to, created_by)
    VALUES (SCOPE_IDENTITY(), @stallId, 2.50, 10, 0, 4, 'new', DATEADD(HOUR, -25, @now), NULL, @farmerId);
END;

/* ---------- 2) Sold-out product ---------- */
IF NOT EXISTS (SELECT 1 FROM Products WHERE farmer_id = @farmerId AND product_name = N'Test Sold Out Bananas')
BEGIN
    INSERT INTO Products (farmer_id, category_id, exp_id, product_name, description, unit, image_url,
                          published_at, expires_at, status, created_at)
    VALUES (@farmerId, @categoryId, @longExpId, N'Test Sold Out Bananas', N'Test data: listing still running, but nothing left to sell.',
            N'bunch', N'/images/products/bananas.jpg',
            DATEADD(DAY, -1, @now),
            DATEADD(DAY, 5, @now),         -- still within the listing period
            'active', DATEADD(DAY, -1, @now));

    INSERT INTO Stock_Price (product_id, stall_id, price, quantity_in, quantity_reserved, quantity_sold,
                             change_type, effective_from, effective_to, created_by)
    VALUES (SCOPE_IDENTITY(), @stallId, 1.80, 8, 0, 8, 'new', DATEADD(DAY, -1, @now), NULL, @farmerId);  -- 8 in, 8 sold = 0 left
END;
GO

/* ---------- Check ---------- */
SELECT p.product_id, p.product_name, p.status, e.exp_code, p.expires_at,
       sp.price, sp.quantity_in, sp.quantity_reserved, sp.quantity_sold,
       sp.quantity_in - sp.quantity_reserved - sp.quantity_sold AS quantity_left,
       CASE WHEN p.expires_at <= SYSDATETIME() THEN 'EXPIRED'
            WHEN sp.quantity_in - sp.quantity_reserved - sp.quantity_sold <= 0 THEN 'SOLD OUT'
            ELSE 'ON SALE' END AS test_case
FROM Products p
JOIN Product_Exp e ON e.exp_id = p.exp_id
LEFT JOIN Stock_Price sp ON sp.product_id = p.product_id AND sp.effective_to IS NULL
WHERE p.farmer_id = 3 AND p.product_name LIKE N'Test %';
