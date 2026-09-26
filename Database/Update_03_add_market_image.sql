/* =====================================================================
   Update for a MarketDB created before this change
   - Markets: add image_url (market photo)
   Run once, after Update_02. A new database created from MarketLink.sql
   already has this column.
   ===================================================================== */

USE MarketDB;
GO

ALTER TABLE Markets ADD image_url NVARCHAR(500) NULL;   -- e.g. /uploads/markets/abc.jpg
GO
