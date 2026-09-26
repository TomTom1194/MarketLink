/* =====================================================================
   Update for a MarketDB created before this change
   - Markets: drop address, map_url becomes required
   Run once, after Update_01. A new database created from MarketLink.sql
   already has these changes.
   ===================================================================== */

USE MarketDB;
GO

ALTER TABLE Markets DROP COLUMN address;
GO

-- map_url is now required. If a market has no link yet, this step fails:
-- run   SELECT market_id, market_name FROM Markets WHERE map_url IS NULL;
-- add the missing links, then run this step again.
ALTER TABLE Markets ALTER COLUMN map_url NVARCHAR(500) NOT NULL;
GO
