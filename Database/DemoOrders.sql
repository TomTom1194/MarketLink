/* Run after MarketLink.sql and after approving a farmer.
   Re-runnable. Demo customer password: admin@123.
   Search the farmer's orders using 0909000011 through 0909000020. */
USE MarketDB;
GO
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

DECLARE @farmerId INT = (SELECT TOP (1) farmer_id FROM Farmer_Profile WHERE approval_status = 'approved' ORDER BY farmer_id);
IF @farmerId IS NULL THROW 51000, 'Approve a farmer before loading demo orders.', 1;
DECLARE @districtId INT = (SELECT district_id FROM Farmer_Profile WHERE farmer_id = @farmerId);
DECLARE @customerRoleId INT = (SELECT role_id FROM Role WHERE role_name = 'customer');
IF @customerRoleId IS NULL THROW 51001, 'Customer role is missing.', 1;

DECLARE @marketId INT = (SELECT TOP (1) market_id FROM Markets WHERE market_name = N'Chợ MarketLink Demo' ORDER BY market_id);
IF @marketId IS NULL
BEGIN
    INSERT INTO Markets (district_id, market_name, address, open_days, open_time, close_time, is_active)
    VALUES (@districtId, N'Chợ MarketLink Demo', N'TP. Hồ Chí Minh', N'1,2,3,4,5,6,7', '06:00', '20:00', 1);
    SET @marketId = CONVERT(INT, SCOPE_IDENTITY());
END;
DECLARE @stallId INT = (SELECT stall_id FROM Stalls WHERE market_id = @marketId AND stall_code = 'DEMO-10');
IF @stallId IS NULL
BEGIN
    INSERT INTO Stalls (market_id, farmer_id, stall_code, location_note, selling_days, is_active)
    VALUES (@marketId, @farmerId, 'DEMO-10', N'Gian demo tra đơn theo số điện thoại', N'1,2,3,4,5,6,7', 1);
    SET @stallId = CONVERT(INT, SCOPE_IDENTITY());
END;
DECLARE @categoryId INT = (SELECT category_id FROM Product_Category WHERE slug = 'demo-10-vegetables');
IF @categoryId IS NULL
BEGIN
    INSERT INTO Product_Category (category_name, slug, is_active) VALUES (N'Rau củ demo', 'demo-10-vegetables', 1);
    SET @categoryId = CONVERT(INT, SCOPE_IDENTITY());
END;
DECLARE @expId INT = (SELECT exp_id FROM Product_Exp WHERE exp_code = 'D10');
IF @expId IS NULL
BEGIN
    INSERT INTO Product_Exp (exp_code, exp_name, duration_days, description)
    VALUES ('D10', N'Demo 10 khách', 365, N'Sản phẩm dùng để thử quy trình đơn hàng.');
    SET @expId = CONVERT(INT, SCOPE_IDENTITY());
END;

DECLARE @products TABLE (product_name NVARCHAR(150), price DECIMAL(12,2));
INSERT INTO @products VALUES (N'Cà chua demo 10 khách', 35000), (N'Rau muống demo 10 khách', 25000), (N'Cà rốt demo 10 khách', 30000);
INSERT INTO Products (farmer_id, category_id, exp_id, product_name, description, unit, image_url, published_at, expires_at, status)
SELECT @farmerId, @categoryId, @expId, seed.product_name, N'Nông sản demo.', N'kg', '', SYSDATETIME(), DATEADD(day, 365, SYSDATETIME()), 'active'
FROM @products AS seed
WHERE NOT EXISTS (SELECT 1 FROM Products AS p WHERE p.farmer_id = @farmerId AND p.product_name = seed.product_name);
INSERT INTO Stock_Price (product_id, stall_id, price, quantity_in, quantity_reserved, quantity_sold, created_by)
SELECT p.product_id, @stallId, seed.price, 100, 0, 0, @farmerId
FROM @products AS seed JOIN Products AS p ON p.farmer_id = @farmerId AND p.product_name = seed.product_name
WHERE NOT EXISTS (SELECT 1 FROM Stock_Price AS stock WHERE stock.product_id = p.product_id AND stock.stall_id = @stallId AND stock.effective_to IS NULL);

DECLARE @customers TABLE (n INT PRIMARY KEY, full_name NVARCHAR(100), phone NVARCHAR(15));
INSERT INTO @customers VALUES
    (1, N'Nguyễn Thị Lan', '0909000011'), (2, N'Trần Văn Minh', '0909000012'),
    (3, N'Lê Thị Hương', '0909000013'), (4, N'Phạm Quốc Bảo', '0909000014'),
    (5, N'Hoàng Thị Mai', '0909000015'), (6, N'Võ Văn Nam', '0909000016'),
    (7, N'Đặng Ngọc Anh', '0909000017'), (8, N'Bùi Thanh Tùng', '0909000018'),
    (9, N'Đỗ Thị Hoa', '0909000019'), (10, N'Ngô Quang Huy', '0909000020');
IF EXISTS (SELECT 1 FROM @customers AS demo JOIN Users AS u ON u.phone = demo.phone
           WHERE u.email <> CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local'))
    THROW 51002, 'A demo phone number already belongs to another account.', 1;
IF EXISTS (SELECT 1 FROM @customers AS demo
           JOIN Users AS u ON u.email = CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local')
           WHERE u.phone <> demo.phone OR u.role_id <> @customerRoleId)
    THROW 51003, 'A demo email already belongs to a different customer or phone.', 1;
INSERT INTO Users (role_id, email, password_hash, phone, status)
SELECT @customerRoleId, CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local'),
       '$2a$11$X1WjR5ZNkRVH5DvkrfO7W.9MaJm.4y/AeS.EEoVJ84ruXiv5m/zIa', demo.phone, 'active'
FROM @customers AS demo
WHERE NOT EXISTS (SELECT 1 FROM Users AS u WHERE u.email = CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local'));
INSERT INTO Customer_Profile (customer_id, full_name, address, district_id)
SELECT u.user_id, demo.full_name, N'Địa chỉ demo, TP. Hồ Chí Minh', @districtId
FROM @customers AS demo JOIN Users AS u ON u.email = CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local')
WHERE NOT EXISTS (SELECT 1 FROM Customer_Profile AS profile WHERE profile.customer_id = u.user_id);

DECLARE @orders TABLE (n INT PRIMARY KEY, status NVARCHAR(20), product_name NVARCHAR(150), quantity DECIMAL(10,2));
INSERT INTO @orders VALUES
    (1, 'placed', N'Cà chua demo 10 khách', 2), (2, 'placed', N'Rau muống demo 10 khách', 1),
    (3, 'placed', N'Cà rốt demo 10 khách', 3), (4, 'accepted', N'Cà chua demo 10 khách', 1),
    (5, 'accepted', N'Rau muống demo 10 khách', 2), (6, 'accepted', N'Cà rốt demo 10 khách', 2),
    (7, 'completed', N'Cà chua demo 10 khách', 3), (8, 'completed', N'Rau muống demo 10 khách', 2),
    (9, 'completed', N'Cà rốt demo 10 khách', 1), (10, 'rejected', N'Cà chua demo 10 khách', 1);
DECLARE @newOrders TABLE (order_id INT PRIMARY KEY, order_code NVARCHAR(20));
INSERT INTO Orders (order_code, customer_id, stall_id, pickup_date, pickup_from, pickup_to,
                    pickup_name, pickup_phone, status, total_amount, reject_reason,
                    placed_at, accepted_at, rejected_at, completed_at, completed_by)
OUTPUT inserted.order_id, inserted.order_code INTO @newOrders (order_id, order_code)
SELECT CONCAT('DEMO-10-', RIGHT(CONCAT('00', seed.n), 2)), u.user_id, @stallId,
       DATEADD(day, CASE WHEN seed.status = 'completed' THEN -1 ELSE 1 END, CONVERT(date, SYSDATETIME())),
       '08:00', '10:00', demo.full_name, demo.phone, seed.status, stock.price * seed.quantity,
       CASE WHEN seed.status = 'rejected' THEN N'Hết hàng trong khung giờ đã chọn.' END,
       DATEADD(day, CASE WHEN seed.status = 'completed' THEN -2 ELSE -1 END, SYSDATETIME()),
       CASE WHEN seed.status IN ('accepted', 'completed') THEN DATEADD(hour, -3, SYSDATETIME()) END,
       CASE WHEN seed.status = 'rejected' THEN DATEADD(hour, -2, SYSDATETIME()) END,
       CASE WHEN seed.status = 'completed' THEN DATEADD(hour, -1, SYSDATETIME()) END,
       CASE WHEN seed.status = 'completed' THEN @farmerId END
FROM @orders AS seed JOIN @customers AS demo ON demo.n = seed.n
JOIN Users AS u ON u.email = CONCAT('demo.customer', RIGHT(CONCAT('00', demo.n), 2), '@marketlink.local')
JOIN Products AS p ON p.farmer_id = @farmerId AND p.product_name = seed.product_name
JOIN Stock_Price AS stock ON stock.product_id = p.product_id AND stock.stall_id = @stallId AND stock.effective_to IS NULL
WHERE NOT EXISTS (SELECT 1 FROM Orders AS existing WHERE existing.order_code = CONCAT('DEMO-10-', RIGHT(CONCAT('00', seed.n), 2)));
INSERT INTO Order_Snapshot (order_id, product_id, stock_price_id, product_name, category_name, unit, image_url, unit_price, quantity)
SELECT newly.order_id, p.product_id, stock.stock_price_id, p.product_name, category.category_name, p.unit, p.image_url, stock.price, seed.quantity
FROM @newOrders AS newly JOIN @orders AS seed ON newly.order_code = CONCAT('DEMO-10-', RIGHT(CONCAT('00', seed.n), 2))
JOIN Products AS p ON p.farmer_id = @farmerId AND p.product_name = seed.product_name
JOIN Product_Category AS category ON category.category_id = p.category_id
JOIN Stock_Price AS stock ON stock.product_id = p.product_id AND stock.stall_id = @stallId AND stock.effective_to IS NULL;
UPDATE stock SET stock.quantity_reserved = stock.quantity_reserved + totals.reserved,
                 stock.quantity_sold = stock.quantity_sold + totals.sold
FROM Stock_Price AS stock JOIN (
    SELECT item.stock_price_id,
           SUM(CASE WHEN o.status IN ('placed', 'accepted') THEN item.quantity ELSE 0 END) AS reserved,
           SUM(CASE WHEN o.status = 'completed' THEN item.quantity ELSE 0 END) AS sold
    FROM @newOrders AS newly JOIN Orders AS o ON o.order_id = newly.order_id
    JOIN Order_Snapshot AS item ON item.order_id = o.order_id GROUP BY item.stock_price_id
) AS totals ON totals.stock_price_id = stock.stock_price_id;
INSERT INTO Notifications (user_id, order_id, type, title, body, created_at)
SELECT @farmerId, o.order_id, 'new_order', CONCAT(N'Đơn hàng mới #', o.order_code),
       CONCAT(o.pickup_name, N' đã đặt hàng. Hãy xem và xử lý đơn.'), o.placed_at
FROM @newOrders AS newly JOIN Orders AS o ON o.order_id = newly.order_id;
INSERT INTO Notifications (user_id, order_id, type, title, body, created_at)
SELECT o.customer_id, o.order_id,
       CASE o.status WHEN 'accepted' THEN 'order_accepted' WHEN 'completed' THEN 'order_completed' ELSE 'order_rejected' END,
       CONCAT(N'Đơn hàng #', o.order_code, N' đã ',
              CASE o.status WHEN 'accepted' THEN N'được xác nhận' WHEN 'completed' THEN N'hoàn thành' ELSE N'bị từ chối' END),
       CASE WHEN o.status = 'rejected' THEN CONCAT(N'Lý do: ', o.reject_reason)
            ELSE N'Xem chi tiết đơn hàng để biết thêm thông tin.' END,
       COALESCE(o.completed_at, o.rejected_at, o.accepted_at)
FROM @newOrders AS newly JOIN Orders AS o ON o.order_id = newly.order_id
WHERE o.status IN ('accepted', 'completed', 'rejected');
COMMIT TRANSACTION;

SELECT COUNT(*) AS DemoOrderCount, COUNT(DISTINCT pickup_phone) AS DistinctPhoneCount
FROM Orders WHERE order_code LIKE 'DEMO-10-%';
SELECT order_code, pickup_name, pickup_phone, status, total_amount
FROM Orders WHERE order_code LIKE 'DEMO-10-%' ORDER BY order_code;
