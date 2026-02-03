-- =====================================================
-- 點燈系統 Stored Procedures
-- 注意：請在資料庫已存在的前提下執行此腳本
-- =====================================================

-- =====================================================
-- sp_TryLockLampSlot: 嘗試鎖定燈位
-- 使用 UPDLOCK, ROWLOCK 確保並發安全
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_TryLockLampSlot]') AND type in (N'P'))
    DROP PROCEDURE [dbo].[sp_TryLockLampSlot]
GO

CREATE PROCEDURE [dbo].[sp_TryLockLampSlot]
    @SlotId INT,
    @WorkstationId NVARCHAR(50),
    @LockDurationSeconds INT = 300,
    @Success BIT OUTPUT,
    @FailureReason NVARCHAR(200) OUTPUT,
    @LockExpiresAt DATETIME OUTPUT,
    @Price DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CurrentStatus NVARCHAR(20);
    DECLARE @CurrentLocker NVARCHAR(50);
    DECLARE @CurrentExpiry DATETIME;
    DECLARE @CurrentPrice DECIMAL(18,2);
    DECLARE @Now DATETIME = GETDATE();

    -- 初始化輸出參數
    SET @Success = 0;
    SET @FailureReason = NULL;
    SET @LockExpiresAt = NULL;
    SET @Price = NULL;

    BEGIN TRANSACTION;

    -- 使用 UPDLOCK, ROWLOCK 鎖定該燈位記錄（同時取得價格）
    SELECT
        @CurrentStatus = Status,
        @CurrentLocker = LockedByWorkstation,
        @CurrentExpiry = LockExpiresAt,
        @CurrentPrice = Price
    FROM LampSlots WITH (UPDLOCK, ROWLOCK)
    WHERE SlotId = @SlotId;

    -- 檢查燈位是否存在
    IF @CurrentStatus IS NULL
    BEGIN
        SET @FailureReason = N'燈位不存在';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- 檢查燈位狀態
    IF @CurrentStatus = 'SOLD'
    BEGIN
        SET @FailureReason = N'燈位已售出 (SOLD)';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- 如果已被鎖定
    IF @CurrentStatus = 'LOCKED'
    BEGIN
        -- 檢查是否為同一工作站（允許延長鎖定）
        IF @CurrentLocker = @WorkstationId
        BEGIN
            -- 同一工作站，延長鎖定時間
            SET @LockExpiresAt = DATEADD(SECOND, @LockDurationSeconds, @Now);

            UPDATE LampSlots
            SET LockExpiresAt = @LockExpiresAt,
                UpdatedAt = @Now
            WHERE SlotId = @SlotId;

            SET @Success = 1;
            SET @Price = @CurrentPrice;
            COMMIT TRANSACTION;
            RETURN;
        END

        -- 檢查鎖定是否已過期
        IF @CurrentExpiry IS NOT NULL AND @CurrentExpiry < @Now
        BEGIN
            -- 鎖定已過期，可以重新鎖定
            SET @LockExpiresAt = DATEADD(SECOND, @LockDurationSeconds, @Now);

            UPDATE LampSlots
            SET Status = 'LOCKED',
                LockedByWorkstation = @WorkstationId,
                LockExpiresAt = @LockExpiresAt,
                UpdatedAt = @Now
            WHERE SlotId = @SlotId;

            SET @Success = 1;
            SET @Price = @CurrentPrice;
            COMMIT TRANSACTION;
            RETURN;
        END

        -- 被其他工作站鎖定且尚未過期
        SET @FailureReason = N'燈位已被鎖定 (LOCKED by ' + ISNULL(@CurrentLocker, 'Unknown') + N')';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- 狀態為 AVAILABLE，可以鎖定
    IF @CurrentStatus = 'AVAILABLE'
    BEGIN
        SET @LockExpiresAt = DATEADD(SECOND, @LockDurationSeconds, @Now);

        UPDATE LampSlots
        SET Status = 'LOCKED',
            LockedByWorkstation = @WorkstationId,
            LockExpiresAt = @LockExpiresAt,
            UpdatedAt = @Now
        WHERE SlotId = @SlotId;

        SET @Success = 1;
        SET @Price = @CurrentPrice;
        COMMIT TRANSACTION;
        RETURN;
    END

    -- 其他未知狀態
    SET @FailureReason = N'燈位狀態異常: ' + ISNULL(@CurrentStatus, 'NULL');
    ROLLBACK TRANSACTION;
END
GO

-- =====================================================
-- sp_GenerateOrderNumber: 產生訂單編號
-- 格式: ORD-YYYYMMDD-NNNN
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GenerateOrderNumber]') AND type in (N'P'))
    DROP PROCEDURE [dbo].[sp_GenerateOrderNumber]
GO

CREATE PROCEDURE [dbo].[sp_GenerateOrderNumber]
    @OrderNumber NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @DatePart NVARCHAR(8) = FORMAT(@Today, 'yyyyMMdd');
    DECLARE @Sequence INT;
    DECLARE @MaxOrderNumber NVARCHAR(50);

    -- 取得今日最大流水號
    SELECT @MaxOrderNumber = MAX(OrderNumber)
    FROM Orders
    WHERE OrderNumber LIKE 'ORD-' + @DatePart + '-%';

    IF @MaxOrderNumber IS NULL
    BEGIN
        SET @Sequence = 1;
    END
    ELSE
    BEGIN
        -- 從訂單編號中取出流水號部分
        SET @Sequence = CAST(RIGHT(@MaxOrderNumber, 4) AS INT) + 1;
    END

    -- 組合訂單編號
    SET @OrderNumber = 'ORD-' + @DatePart + '-' + RIGHT('0000' + CAST(@Sequence AS NVARCHAR(4)), 4);
END
GO

-- =====================================================
-- sp_CleanupExpiredLocks: 清理過期的燈位鎖定
-- 可透過 SQL Agent Job 定期執行
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_CleanupExpiredLocks]') AND type in (N'P'))
    DROP PROCEDURE [dbo].[sp_CleanupExpiredLocks]
GO

CREATE PROCEDURE [dbo].[sp_CleanupExpiredLocks]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AffectedCount INT;

    UPDATE LampSlots
    SET Status = 'AVAILABLE',
        LockedByWorkstation = NULL,
        LockExpiresAt = NULL,
        UpdatedAt = GETDATE()
    WHERE Status = 'LOCKED'
      AND LockExpiresAt < GETDATE();

    SET @AffectedCount = @@ROWCOUNT;

    IF @AffectedCount > 0
    BEGIN
        -- 記錄到 AuditLogs
        INSERT INTO AuditLogs (Action, EntityType, EntityId, WorkstationId, Details, CreatedAt)
        VALUES ('CLEANUP', 'LampSlot', 0, 'SYSTEM',
                N'自動清理過期鎖定，共 ' + CAST(@AffectedCount AS NVARCHAR(10)) + N' 個燈位',
                GETDATE());
    END

    SELECT @AffectedCount AS CleanedCount;
END
GO

PRINT N'Stored Procedures 建立完成';
GO
