/* =====================================================================
   Update for a MarketDB created before this change
   - Markets: add requested_by (farmer who suggested the market)
   Run once, after Update_03. A new database created from MarketLink.sql
   already has this column.
   ===================================================================== */

USE MarketDB;
GO

ALTER TABLE Markets ADD requested_by INT NULL REFERENCES Users(user_id);
GO
