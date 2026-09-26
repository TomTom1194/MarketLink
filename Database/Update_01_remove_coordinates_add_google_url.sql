/* =====================================================================
   Update for a MarketDB that was created with the old MarketLink.sql
   - Markets: drop latitude, longitude
   - Stalls:  drop latitude, longitude, add google_url
   Run once. A new database created from MarketLink.sql already has these changes.
   ===================================================================== */

USE MarketDB;
GO

ALTER TABLE Markets DROP COLUMN latitude, longitude;
GO

ALTER TABLE Stalls DROP COLUMN latitude, longitude;
GO

ALTER TABLE Stalls ADD google_url NVARCHAR(500) NULL;   -- Google Maps link to the stall
GO
