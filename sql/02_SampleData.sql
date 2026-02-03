-- =====================================================
-- 點燈系統測試資料
-- 執行前請確認資料表已建立
-- =====================================================
USE MyAppDb;
GO

-- =====================================================
-- 清除既有測試資料（可選）
-- =====================================================
-- DELETE FROM AuditLogs;
-- DELETE FROM Payments;
-- DELETE FROM OrderItems;
-- DELETE FROM Orders;
-- DELETE FROM Customers;
-- DELETE FROM LampSlots;
-- DELETE FROM LampTypes;

-- =====================================================
-- 燈種資料
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM LampTypes WHERE Name = N'光明燈')
BEGIN
    INSERT INTO LampTypes (Name, Description, DefaultPrice, IsActive, SortOrder)
    VALUES
        (N'光明燈', N'祈求光明、智慧開啟', 600.00, 1, 1),
        (N'平安燈', N'祈求闔家平安、身體健康', 600.00, 1, 2),
        (N'財神燈', N'祈求財運亨通、事業順利', 1000.00, 1, 3),
        (N'太歲燈', N'安奉太歲、化解沖煞', 800.00, 1, 4),
        (N'姻緣燈', N'祈求姻緣美滿、感情順利', 600.00, 1, 5),
        (N'文昌燈', N'祈求學業進步、考試順利', 600.00, 1, 6);

    PRINT N'燈種資料新增完成';
END
GO

-- =====================================================
-- 燈位資料 (當年度)
-- =====================================================
DECLARE @CurrentYear INT = YEAR(GETDATE());
DECLARE @LampTypeId INT;
DECLARE @Row INT;
DECLARE @Col INT;
DECLARE @SlotNumber NVARCHAR(20);

-- 光明燈區 (A區，5排 x 10列 = 50個)
SELECT @LampTypeId = LampTypeId FROM LampTypes WHERE Name = N'光明燈';

IF NOT EXISTS (SELECT 1 FROM LampSlots WHERE Zone = N'A區' AND Year = @CurrentYear)
BEGIN
    SET @Row = 1;
    WHILE @Row <= 5
    BEGIN
        SET @Col = 1;
        WHILE @Col <= 10
        BEGIN
            SET @SlotNumber = 'A-' + RIGHT('00' + CAST(@Row AS NVARCHAR(2)), 2) + RIGHT('00' + CAST(@Col AS NVARCHAR(2)), 2);
            INSERT INTO LampSlots (LampTypeId, SlotNumber, Zone, Row, [Column], Year, Price, Status)
            VALUES (@LampTypeId, @SlotNumber, N'A區', @Row, @Col, @CurrentYear, 600.00, 'AVAILABLE');
            SET @Col = @Col + 1;
        END
        SET @Row = @Row + 1;
    END
    PRINT N'光明燈區 (A區) 燈位新增完成';
END

-- 平安燈區 (B區，5排 x 10列 = 50個)
SELECT @LampTypeId = LampTypeId FROM LampTypes WHERE Name = N'平安燈';

IF NOT EXISTS (SELECT 1 FROM LampSlots WHERE Zone = N'B區' AND Year = @CurrentYear)
BEGIN
    SET @Row = 1;
    WHILE @Row <= 5
    BEGIN
        SET @Col = 1;
        WHILE @Col <= 10
        BEGIN
            SET @SlotNumber = 'B-' + RIGHT('00' + CAST(@Row AS NVARCHAR(2)), 2) + RIGHT('00' + CAST(@Col AS NVARCHAR(2)), 2);
            INSERT INTO LampSlots (LampTypeId, SlotNumber, Zone, Row, [Column], Year, Price, Status)
            VALUES (@LampTypeId, @SlotNumber, N'B區', @Row, @Col, @CurrentYear, 600.00, 'AVAILABLE');
            SET @Col = @Col + 1;
        END
        SET @Row = @Row + 1;
    END
    PRINT N'平安燈區 (B區) 燈位新增完成';
END

-- 財神燈區 (C區，3排 x 8列 = 24個)
SELECT @LampTypeId = LampTypeId FROM LampTypes WHERE Name = N'財神燈';

IF NOT EXISTS (SELECT 1 FROM LampSlots WHERE Zone = N'C區' AND Year = @CurrentYear)
BEGIN
    SET @Row = 1;
    WHILE @Row <= 3
    BEGIN
        SET @Col = 1;
        WHILE @Col <= 8
        BEGIN
            SET @SlotNumber = 'C-' + RIGHT('00' + CAST(@Row AS NVARCHAR(2)), 2) + RIGHT('00' + CAST(@Col AS NVARCHAR(2)), 2);
            INSERT INTO LampSlots (LampTypeId, SlotNumber, Zone, Row, [Column], Year, Price, Status)
            VALUES (@LampTypeId, @SlotNumber, N'C區', @Row, @Col, @CurrentYear, 1000.00, 'AVAILABLE');
            SET @Col = @Col + 1;
        END
        SET @Row = @Row + 1;
    END
    PRINT N'財神燈區 (C區) 燈位新增完成';
END

-- 太歲燈區 (D區，4排 x 8列 = 32個)
SELECT @LampTypeId = LampTypeId FROM LampTypes WHERE Name = N'太歲燈';

IF NOT EXISTS (SELECT 1 FROM LampSlots WHERE Zone = N'D區' AND Year = @CurrentYear)
BEGIN
    SET @Row = 1;
    WHILE @Row <= 4
    BEGIN
        SET @Col = 1;
        WHILE @Col <= 8
        BEGIN
            SET @SlotNumber = 'D-' + RIGHT('00' + CAST(@Row AS NVARCHAR(2)), 2) + RIGHT('00' + CAST(@Col AS NVARCHAR(2)), 2);
            INSERT INTO LampSlots (LampTypeId, SlotNumber, Zone, Row, [Column], Year, Price, Status)
            VALUES (@LampTypeId, @SlotNumber, N'D區', @Row, @Col, @CurrentYear, 800.00, 'AVAILABLE');
            SET @Col = @Col + 1;
        END
        SET @Row = @Row + 1;
    END
    PRINT N'太歲燈區 (D區) 燈位新增完成';
END
GO

-- =====================================================
-- 測試客戶資料
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM Customers WHERE Phone = '0912345678')
BEGIN
    INSERT INTO Customers (Name, Phone, Address, Notes)
    VALUES
        (N'王小明', '0912345678', N'台北市中正區忠孝東路一段1號', N'VIP客戶'),
        (N'李大華', '0923456789', N'新北市板橋區中山路100號', NULL),
        (N'張美麗', '0934567890', N'台北市信義區信義路五段7號', N'每年固定點燈');

    PRINT N'測試客戶資料新增完成';
END
GO

-- =====================================================
-- 確認資料筆數
-- =====================================================
SELECT N'燈種數量' AS [項目], COUNT(*) AS [筆數] FROM LampTypes
UNION ALL
SELECT N'當年度燈位數量', COUNT(*) FROM LampSlots WHERE Year = YEAR(GETDATE())
UNION ALL
SELECT N'可用燈位數量', COUNT(*) FROM LampSlots WHERE Year = YEAR(GETDATE()) AND Status = 'AVAILABLE'
UNION ALL
SELECT N'客戶數量', COUNT(*) FROM Customers;
GO

PRINT N'測試資料載入完成';
GO
