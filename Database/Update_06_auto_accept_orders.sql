/* For databases installed from MarketLink.sql or a BACPAC.
   EF migration AutoAcceptOrders adds the same columns; do not run both manually. */
USE MarketDB;
GO

IF COL_LENGTH('dbo.Stalls', 'auto_accept_enabled') IS NULL
    ALTER TABLE dbo.Stalls ADD auto_accept_enabled BIT NOT NULL
        CONSTRAINT DF_Stalls_auto_accept_enabled DEFAULT (0);

IF COL_LENGTH('dbo.Stalls', 'auto_accept_enabled_at') IS NULL
    ALTER TABLE dbo.Stalls ADD auto_accept_enabled_at DATETIME2 NULL;

IF COL_LENGTH('dbo.Orders', 'auto_accept_failed_at') IS NULL
    ALTER TABLE dbo.Orders ADD auto_accept_failed_at DATETIME2 NULL;
