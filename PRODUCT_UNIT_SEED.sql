USE [MarketDB];
GO

-- Danh mục đơn vị cho form sản phẩm. Không thay đổi các bản ghi Products hiện có.
IF OBJECT_ID(N'dbo.Product_Unit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Product_Unit
    (
        unit NVARCHAR(20) NOT NULL CONSTRAINT PK_Product_Unit PRIMARY KEY,
        sort_order INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_Product_Unit_IsActive DEFAULT (1)
    );
END;
GO

WITH Units(unit, sort_order) AS
(
    SELECT unit, sort_order FROM (VALUES
        (N'kg', 1), (N'g', 2), (N'lạng', 3), (N'tạ', 4),
        (N'tấn', 5), (N'lít', 6), (N'ml', 7), (N'bó', 8),
        (N'mớ', 9), (N'củ', 10), (N'quả', 11), (N'trái', 12),
        (N'chiếc', 13), (N'cái', 14), (N'túi', 15),
        (N'hộp', 16), (N'thùng', 17)
    ) AS source(unit, sort_order)
)
INSERT INTO dbo.Product_Unit(unit, sort_order)
SELECT source.unit, source.sort_order
FROM Units AS source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product_Unit AS existing WHERE existing.unit = source.unit);
GO

SELECT unit, sort_order, is_active FROM dbo.Product_Unit ORDER BY sort_order;
