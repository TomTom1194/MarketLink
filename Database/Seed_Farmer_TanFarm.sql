/* =====================================================================
   Test farmer account: Tan Farm
     Email:    tan@marketlink.vn
     Password: 123      (stored as a BCrypt hash, like every other account)
   Creates: Users (role farmer) + Farmer_Profile (approved) + 1 active stall,
   so the farmer can log in and sell straight away.
   Safe to re-run: nothing is added twice.
   ===================================================================== */
USE MarketDB;
GO

DECLARE @email    NVARCHAR(255) = N'tan@marketlink.vn';
DECLARE @phone    NVARCHAR(15)  = N'0909123456';
DECLARE @hash     NVARCHAR(255) = N'$2a$11$OssKRPfNxKxZFpiylFmjH.QSWvfJLeOjY0azk1azG3jV7KSE1PmeG';  -- "123"

DECLARE @farmerRoleId INT = (SELECT role_id FROM Role WHERE role_name = 'farmer');
DECLARE @adminId      INT = (SELECT TOP 1 u.user_id FROM Users u JOIN Role r ON r.role_id = u.role_id WHERE r.role_name = 'admin' ORDER BY u.user_id);

-- The stall goes into the first active market; the farm address uses the same district
DECLARE @marketId   INT = (SELECT TOP 1 market_id FROM Markets WHERE is_active = 1 ORDER BY market_id);
DECLARE @districtId INT = (SELECT district_id FROM Markets WHERE market_id = @marketId);
DECLARE @openDays   NVARCHAR(20) = (SELECT open_days FROM Markets WHERE market_id = @marketId);

IF @marketId IS NULL
    THROW 50000, 'There is no active market yet. Create one in Admin > Markets first.', 1;

IF EXISTS (SELECT 1 FROM Users WHERE phone = @phone AND email <> @email)
    THROW 50001, 'Phone 0909123456 is already used by another account. Change @phone at the top of this script.', 1;

BEGIN TRANSACTION;

/* 1) Login account */
IF NOT EXISTS (SELECT 1 FROM Users WHERE email = @email)
    INSERT INTO Users (role_id, email, password_hash, phone, status, created_at)
    VALUES (@farmerRoleId, @email, @hash, @phone, 'active', SYSDATETIME());

DECLARE @userId INT = (SELECT user_id FROM Users WHERE email = @email);

/* 2) Farmer profile, already approved */
IF NOT EXISTS (SELECT 1 FROM Farmer_Profile WHERE farmer_id = @userId)
    INSERT INTO Farmer_Profile (farmer_id, brand_name, contact_person, address, district_id, description,
                                approval_status, approved_by, approved_at, created_at)
    VALUES (@userId, N'Tan Farm', N'Tan', N'12 Market Street', @districtId,
            N'Test farmer account: fresh vegetables and fruit.',
            'approved', @adminId, SYSDATETIME(), SYSDATETIME());

/* 3) One active stall, selling on every day the market is open */
IF NOT EXISTS (SELECT 1 FROM Stalls WHERE farmer_id = @userId)
    INSERT INTO Stalls (market_id, farmer_id, stall_code, location_note, selling_days, is_active)
    VALUES (@marketId, @userId, N'TAN-01', N'Test stall', @openDays, 1);

COMMIT;
GO

/* Check */
SELECT u.user_id AS farmer_id, u.email, u.phone, u.status,
       f.brand_name, f.approval_status,
       s.stall_code, m.market_name, s.selling_days
FROM Users u
JOIN Farmer_Profile f ON f.farmer_id = u.user_id
LEFT JOIN Stalls s ON s.farmer_id = u.user_id
LEFT JOIN Markets m ON m.market_id = s.market_id
WHERE u.email = N'tan@marketlink.vn';
