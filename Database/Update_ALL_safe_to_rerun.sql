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

-- Check: every column the code needs should be listed here
SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME = 'Markets' AND COLUMN_NAME IN ('map_url', 'image_url', 'requested_by'))
   OR (TABLE_NAME = 'Stalls'  AND COLUMN_NAME IN ('google_url'))
ORDER BY TABLE_NAME, COLUMN_NAME;
