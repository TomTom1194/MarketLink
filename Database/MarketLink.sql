/* =====================================================================
   MarketLink – create database (SQL Server)
   Run this whole file in SSMS / Azure Data Studio / Rider.
   Order: create database -> create 17 tables -> insert Role, admin, Cities, Districts.
   ===================================================================== */

IF DB_ID('MarketDB') IS NULL
    CREATE DATABASE MarketDB;
GO

USE MarketDB;
GO

/* ---------- A. Accounts, locations, stalls ---------- */

CREATE TABLE Role (
    role_id     INT IDENTITY(1,1) PRIMARY KEY,
    role_name   NVARCHAR(20) NOT NULL UNIQUE          -- customer | farmer | admin
);

CREATE TABLE Users (
    user_id       INT IDENTITY(1,1) PRIMARY KEY,
    role_id       INT NOT NULL REFERENCES Role(role_id),
    email         NVARCHAR(255) NOT NULL UNIQUE,
    password_hash NVARCHAR(255) NOT NULL,
    phone         NVARCHAR(15)  NOT NULL UNIQUE,      -- digits only, e.g. 0901234567
    status        NVARCHAR(20)  NOT NULL DEFAULT 'active'
                  CHECK (status IN ('active', 'disabled')),
    created_at    DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    updated_at    DATETIME2 NULL
);

CREATE TABLE Cities (
    city_id     INT IDENTITY(1,1) PRIMARY KEY,
    city_name   NVARCHAR(100) NOT NULL,
    city_code   NVARCHAR(10)  NOT NULL UNIQUE          -- HCM, HN
);

CREATE TABLE Districts (
    district_id   INT IDENTITY(1,1) PRIMARY KEY,
    city_id       INT NOT NULL REFERENCES Cities(city_id),
    district_name NVARCHAR(100) NOT NULL,
    district_code NVARCHAR(20)  NOT NULL UNIQUE        -- HCM-Q7, HN-CG
);

-- Customer profile, 1-1 with Users (customer_id = user_id)
CREATE TABLE Customer_Profile (
    customer_id  INT PRIMARY KEY REFERENCES Users(user_id),
    full_name    NVARCHAR(100) NOT NULL,
    address      NVARCHAR(255) NOT NULL,
    district_id  INT NOT NULL REFERENCES Districts(district_id),
    created_at   DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);

-- Farmer profile, 1-1 with Users (farmer_id = user_id)
CREATE TABLE Farmer_Profile (
    farmer_id        INT PRIMARY KEY REFERENCES Users(user_id),
    brand_name       NVARCHAR(150) NOT NULL,
    contact_person   NVARCHAR(100) NOT NULL,
    address          NVARCHAR(255) NOT NULL,
    district_id      INT NOT NULL REFERENCES Districts(district_id),
    description      NVARCHAR(MAX) NULL,
    approval_status  NVARCHAR(20) NOT NULL DEFAULT 'pending'
                     CHECK (approval_status IN ('pending', 'approved', 'rejected', 'suspended')),
    approved_by      INT NULL REFERENCES Users(user_id),   -- admin who approved
    approved_at      DATETIME2 NULL,
    created_at       DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE Markets (
    market_id    INT IDENTITY(1,1) PRIMARY KEY,
    district_id  INT NOT NULL REFERENCES Districts(district_id),
    market_name  NVARCHAR(150) NOT NULL,
    map_url      NVARCHAR(500) NOT NULL,             -- Google Maps link to the market
    image_url    NVARCHAR(500) NULL,                 -- market photo, e.g. /uploads/markets/abc.jpg
    open_days    NVARCHAR(20) NOT NULL,              -- market days, e.g. '2,4,7'
    open_time    TIME NOT NULL,
    close_time   TIME NOT NULL,
    is_active    BIT NOT NULL DEFAULT 1,
    requested_by INT NULL REFERENCES Users(user_id),  -- farmer who suggested this market (NULL = created by admin)
    CHECK (close_time > open_time)
);

-- A farmer's stall in a market
CREATE TABLE Stalls (
    stall_id       INT IDENTITY(1,1) PRIMARY KEY,
    market_id      INT NOT NULL REFERENCES Markets(market_id),
    farmer_id      INT NOT NULL REFERENCES Farmer_Profile(farmer_id),
    stall_code     NVARCHAR(20) NOT NULL,             -- e.g. B-12
    location_note  NVARCHAR(255) NULL,                -- e.g. Row B, next to gate 2
    google_url     NVARCHAR(500) NULL,                -- Google Maps link to the stall
    selling_days   NVARCHAR(20) NOT NULL,             -- e.g. '4,7'
    is_active      BIT NOT NULL DEFAULT 1,
    UNIQUE (market_id, stall_code)
);

/* ---------- B. Products, display period, prices ---------- */

CREATE TABLE Product_Category (
    category_id    INT IDENTITY(1,1) PRIMARY KEY,
    parent_id      INT NULL REFERENCES Product_Category(category_id),
    category_name  NVARCHAR(100) NOT NULL,
    slug           NVARCHAR(100) NOT NULL UNIQUE,
    is_active      BIT NOT NULL DEFAULT 1
);

-- How long a product stays visible: Short term / Long term
CREATE TABLE Product_Exp (
    exp_id         INT IDENTITY(1,1) PRIMARY KEY,
    exp_code       NVARCHAR(10) NOT NULL UNIQUE,      -- SHORT | LONG
    exp_name       NVARCHAR(50) NOT NULL,
    duration_days  INT NOT NULL CHECK (duration_days > 0),
    description    NVARCHAR(255) NULL
);

-- Fixed product details; price and quantity live in Stock_Price
CREATE TABLE Products (
    product_id    INT IDENTITY(1,1) PRIMARY KEY,
    farmer_id     INT NOT NULL REFERENCES Farmer_Profile(farmer_id),
    category_id   INT NOT NULL REFERENCES Product_Category(category_id),
    exp_id        INT NOT NULL REFERENCES Product_Exp(exp_id),
    product_name  NVARCHAR(150) NOT NULL,
    description   NVARCHAR(MAX) NULL,
    unit          NVARCHAR(20)  NOT NULL,             -- kg, bunch, box…
    image_url     NVARCHAR(500) NOT NULL,
    published_at  DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    expires_at    DATETIME2 NOT NULL,                 -- published_at + duration_days
    status        NVARCHAR(20) NOT NULL DEFAULT 'active'
                  CHECK (status IN ('active', 'hidden', 'removed')),
    created_at    DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    updated_at    DATETIME2 NULL,
    CHECK (expires_at > published_at)
);

-- Each row is one price + quantity listing; effective_to NULL = on sale now
CREATE TABLE Stock_Price (
    stock_price_id     INT IDENTITY(1,1) PRIMARY KEY,
    product_id         INT NOT NULL REFERENCES Products(product_id),
    stall_id           INT NOT NULL REFERENCES Stalls(stall_id),
    price              DECIMAL(12,2) NOT NULL CHECK (price > 0),
    quantity_in        DECIMAL(10,2) NOT NULL CHECK (quantity_in >= 0),
    quantity_reserved  DECIMAL(10,2) NOT NULL DEFAULT 0,
    quantity_sold      DECIMAL(10,2) NOT NULL DEFAULT 0,
    change_type        NVARCHAR(20) NOT NULL DEFAULT 'new'
                       CHECK (change_type IN ('new', 'price_change', 'restock')),
    effective_from     DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    effective_to       DATETIME2 NULL,
    created_by         INT NOT NULL REFERENCES Users(user_id),
    CHECK (quantity_reserved + quantity_sold <= quantity_in)
);

-- Only one on-sale row per product per stall
CREATE UNIQUE INDEX UX_Stock_Price_current
    ON Stock_Price (product_id, stall_id)
    WHERE effective_to IS NULL;

/* ---------- C. Cart, orders, notifications ---------- */

-- One cart per customer per market
CREATE TABLE Cart (
    cart_id      INT IDENTITY(1,1) PRIMARY KEY,
    customer_id  INT NOT NULL REFERENCES Customer_Profile(customer_id),
    market_id    INT NOT NULL REFERENCES Markets(market_id),
    created_at   DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    updated_at   DATETIME2 NULL,
    UNIQUE (customer_id, market_id)
);

CREATE TABLE Cart_Items (
    cart_item_id    INT IDENTITY(1,1) PRIMARY KEY,
    cart_id         INT NOT NULL REFERENCES Cart(cart_id) ON DELETE CASCADE,
    stock_price_id  INT NOT NULL REFERENCES Stock_Price(stock_price_id),
    quantity        DECIMAL(10,2) NOT NULL CHECK (quantity > 0),
    added_at        DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UNIQUE (cart_id, stock_price_id)
);

-- Pre-order; each order belongs to one stall
CREATE TABLE Orders (
    order_id       INT IDENTITY(1,1) PRIMARY KEY,
    order_code     NVARCHAR(20) NOT NULL UNIQUE,      -- e.g. ML-260925-0042
    customer_id    INT NOT NULL REFERENCES Customer_Profile(customer_id),
    stall_id       INT NOT NULL REFERENCES Stalls(stall_id),
    pickup_date    DATE NOT NULL,
    pickup_from    TIME NOT NULL,
    pickup_to      TIME NOT NULL,
    pickup_name    NVARCHAR(100) NOT NULL,
    pickup_phone   NVARCHAR(15)  NOT NULL,
    status         NVARCHAR(20)  NOT NULL DEFAULT 'placed'
                   CHECK (status IN ('placed', 'accepted', 'rejected', 'cancelled', 'completed', 'no_show')),
    total_amount   DECIMAL(12,2) NOT NULL DEFAULT 0,
    customer_note  NVARCHAR(500) NULL,
    reject_reason  NVARCHAR(500) NULL,                -- required when the farmer rejects
    cancel_reason  NVARCHAR(500) NULL,                -- required when the customer cancels
    placed_at      DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    accepted_at    DATETIME2 NULL,
    rejected_at    DATETIME2 NULL,
    cancelled_at   DATETIME2 NULL,
    completed_at   DATETIME2 NULL,
    completed_by   INT NULL REFERENCES Users(user_id),
    CHECK (pickup_to > pickup_from),
    CHECK (status <> 'rejected'  OR reject_reason IS NOT NULL),
    CHECK (status <> 'cancelled' OR cancel_reason IS NOT NULL)
);

CREATE INDEX IX_Orders_pickup_phone ON Orders (stall_id, pickup_date, pickup_phone);

-- Order line: a copy of the item details at ordering time
CREATE TABLE Order_Snapshot (
    snapshot_id     INT IDENTITY(1,1) PRIMARY KEY,
    order_id        INT NOT NULL REFERENCES Orders(order_id) ON DELETE CASCADE,
    product_id      INT NOT NULL REFERENCES Products(product_id),
    stock_price_id  INT NOT NULL REFERENCES Stock_Price(stock_price_id),
    product_name    NVARCHAR(150) NOT NULL,
    category_name   NVARCHAR(100) NOT NULL,
    unit            NVARCHAR(20)  NOT NULL,
    image_url       NVARCHAR(500) NOT NULL,
    unit_price      DECIMAL(12,2) NOT NULL,
    quantity        DECIMAL(10,2) NOT NULL CHECK (quantity > 0),
    line_total      AS CAST(unit_price * quantity AS DECIMAL(12,2)) PERSISTED,
    UNIQUE (order_id, stock_price_id)
);

CREATE TABLE Notifications (
    notification_id  INT IDENTITY(1,1) PRIMARY KEY,
    user_id          INT NOT NULL REFERENCES Users(user_id),
    order_id         INT NULL REFERENCES Orders(order_id),
    type             NVARCHAR(30) NOT NULL
                     CHECK (type IN ('new_order', 'order_accepted', 'order_rejected', 'order_cancelled', 'order_completed')),
    title            NVARCHAR(150) NOT NULL,
    body             NVARCHAR(500) NULL,
    is_read          BIT NOT NULL DEFAULT 0,
    created_at       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    read_at          DATETIME2 NULL
);
GO

/* =====================================================================
   Initial data
   ===================================================================== */

-- Roles: required for sign-up and permissions
INSERT INTO Role (role_name) VALUES ('customer'), ('farmer'), ('admin');

-- Admin account: email admin@marketlink.vn / password admin@123
-- password_hash is a BCrypt hash (same format as BCrypt.Net-Next); check it with BCrypt.Net.BCrypt.Verify
INSERT INTO Users (role_id, email, password_hash, phone, status)
VALUES (
    (SELECT role_id FROM Role WHERE role_name = 'admin'),
    'admin@marketlink.vn',
    '$2a$11$X1WjR5ZNkRVH5DvkrfO7W.9MaJm.4y/AeS.EEoVJ84ruXiv5m/zIa',
    '0900000000',
    'active'
);

-- Cities
INSERT INTO Cities (city_name, city_code) VALUES
    (N'TP. Hồ Chí Minh', 'HCM'),
    (N'Hà Nội',          'HN');

DECLARE @hcm INT = (SELECT city_id FROM Cities WHERE city_code = 'HCM');
DECLARE @hn  INT = (SELECT city_id FROM Cities WHERE city_code = 'HN');

-- Districts of Ho Chi Minh City (22)
INSERT INTO Districts (city_id, district_name, district_code) VALUES
    (@hcm, N'Quận 1',        'HCM-Q1'),
    (@hcm, N'Quận 3',        'HCM-Q3'),
    (@hcm, N'Quận 4',        'HCM-Q4'),
    (@hcm, N'Quận 5',        'HCM-Q5'),
    (@hcm, N'Quận 6',        'HCM-Q6'),
    (@hcm, N'Quận 7',        'HCM-Q7'),
    (@hcm, N'Quận 8',        'HCM-Q8'),
    (@hcm, N'Quận 10',       'HCM-Q10'),
    (@hcm, N'Quận 11',       'HCM-Q11'),
    (@hcm, N'Quận 12',       'HCM-Q12'),
    (@hcm, N'Bình Thạnh',    'HCM-BTH'),
    (@hcm, N'Gò Vấp',        'HCM-GV'),
    (@hcm, N'Phú Nhuận',     'HCM-PN'),
    (@hcm, N'Tân Bình',      'HCM-TB'),
    (@hcm, N'Tân Phú',       'HCM-TP'),
    (@hcm, N'Bình Tân',      'HCM-BTA'),
    (@hcm, N'TP. Thủ Đức',   'HCM-TD'),
    (@hcm, N'Bình Chánh',    'HCM-BC'),
    (@hcm, N'Cần Giờ',       'HCM-CG'),
    (@hcm, N'Củ Chi',        'HCM-CC'),
    (@hcm, N'Hóc Môn',       'HCM-HM'),
    (@hcm, N'Nhà Bè',        'HCM-NB');

-- Districts of Hanoi (30)
INSERT INTO Districts (city_id, district_name, district_code) VALUES
    (@hn, N'Ba Đình',        'HN-BD'),
    (@hn, N'Hoàn Kiếm',      'HN-HK'),
    (@hn, N'Tây Hồ',         'HN-TH'),
    (@hn, N'Long Biên',      'HN-LB'),
    (@hn, N'Cầu Giấy',       'HN-CG'),
    (@hn, N'Đống Đa',        'HN-DD'),
    (@hn, N'Hai Bà Trưng',   'HN-HBT'),
    (@hn, N'Hoàng Mai',      'HN-HM'),
    (@hn, N'Thanh Xuân',     'HN-TX'),
    (@hn, N'Nam Từ Liêm',    'HN-NTL'),
    (@hn, N'Bắc Từ Liêm',    'HN-BTL'),
    (@hn, N'Hà Đông',        'HN-HD'),
    (@hn, N'Sơn Tây',        'HN-ST'),
    (@hn, N'Ba Vì',          'HN-BV'),
    (@hn, N'Chương Mỹ',      'HN-CM'),
    (@hn, N'Đan Phượng',     'HN-DP'),
    (@hn, N'Đông Anh',       'HN-DA'),
    (@hn, N'Gia Lâm',        'HN-GL'),
    (@hn, N'Hoài Đức',       'HN-HDU'),
    (@hn, N'Mê Linh',        'HN-ML'),
    (@hn, N'Mỹ Đức',         'HN-MD'),
    (@hn, N'Phú Xuyên',      'HN-PX'),
    (@hn, N'Phúc Thọ',       'HN-PT'),
    (@hn, N'Quốc Oai',       'HN-QO'),
    (@hn, N'Sóc Sơn',        'HN-SS'),
    (@hn, N'Thạch Thất',     'HN-TTH'),
    (@hn, N'Thanh Oai',      'HN-TO'),
    (@hn, N'Thanh Trì',      'HN-TTR'),
    (@hn, N'Thường Tín',     'HN-TTI'),
    (@hn, N'Ứng Hòa',        'HN-UH');
GO

-- Check
SELECT c.city_name, COUNT(d.district_id) AS district_count
FROM Cities c LEFT JOIN Districts d ON d.city_id = c.city_id
GROUP BY c.city_name;
