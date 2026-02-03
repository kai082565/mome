-- =====================================================
-- 點燈系統資料庫 Schema 參考
-- 注意：此腳本僅供參考，若 DB 已存在請勿執行
-- =====================================================

-- =====================================================
-- 資料庫建立 (可選)
-- =====================================================
-- CREATE DATABASE MyAppDb COLLATE Chinese_Taiwan_Stroke_CI_AS;
-- GO
-- USE MyAppDb;
-- GO

-- =====================================================
-- 燈種資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LampTypes]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LampTypes] (
        [LampTypeId] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(200) NULL,
        [DefaultPrice] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [SortOrder] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );
END
GO

-- =====================================================
-- 燈位資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LampSlots]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LampSlots] (
        [SlotId] INT IDENTITY(1,1) PRIMARY KEY,
        [LampTypeId] INT NOT NULL,
        [SlotNumber] NVARCHAR(20) NOT NULL,
        [Zone] NVARCHAR(50) NOT NULL,
        [Row] INT NOT NULL DEFAULT 0,
        [Column] INT NOT NULL DEFAULT 0,
        [Year] INT NOT NULL,
        [Price] DECIMAL(10,2) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'AVAILABLE', -- AVAILABLE, LOCKED, SOLD
        [LockedByWorkstation] NVARCHAR(50) NULL,
        [LockExpiresAt] DATETIME NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL,
        CONSTRAINT [FK_LampSlots_LampTypes] FOREIGN KEY ([LampTypeId]) REFERENCES [LampTypes]([LampTypeId])
    );

    -- 索引
    CREATE INDEX [IX_LampSlots_Status] ON [LampSlots]([Status]);
    CREATE INDEX [IX_LampSlots_Year] ON [LampSlots]([Year]);
    CREATE INDEX [IX_LampSlots_LampTypeId] ON [LampSlots]([LampTypeId]);
    CREATE UNIQUE INDEX [IX_LampSlots_SlotNumber_Year] ON [LampSlots]([SlotNumber], [Year]);
END
GO

-- =====================================================
-- 客戶資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Customers] (
        [CustomerId] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(50) NOT NULL,
        [Phone] NVARCHAR(20) NOT NULL,
        [Address] NVARCHAR(200) NULL,
        [Notes] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );

    -- 索引
    CREATE INDEX [IX_Customers_Phone] ON [Customers]([Phone]);
    CREATE INDEX [IX_Customers_Name] ON [Customers]([Name]);
END
GO

-- =====================================================
-- 訂單資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [OrderId] INT IDENTITY(1,1) PRIMARY KEY,
        [OrderNumber] NVARCHAR(50) NOT NULL UNIQUE,
        [CustomerId] INT NOT NULL,
        [LightingName] NVARCHAR(50) NOT NULL,
        [BlessingContent] NVARCHAR(200) NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING, CONFIRMED, CANCELLED
        [TotalAmount] DECIMAL(10,2) NOT NULL,
        [CreatedByWorkstation] NVARCHAR(50) NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL,
        CONSTRAINT [FK_Orders_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [Customers]([CustomerId])
    );

    -- 索引
    CREATE INDEX [IX_Orders_CustomerId] ON [Orders]([CustomerId]);
    CREATE INDEX [IX_Orders_Status] ON [Orders]([Status]);
    CREATE INDEX [IX_Orders_CreatedAt] ON [Orders]([CreatedAt]);
END
GO

-- =====================================================
-- 訂單明細資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderItems] (
        [OrderItemId] INT IDENTITY(1,1) PRIMARY KEY,
        [OrderId] INT NOT NULL,
        [SlotId] INT NOT NULL,
        [UnitPrice] DECIMAL(10,2) NOT NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderId]) REFERENCES [Orders]([OrderId]),
        CONSTRAINT [FK_OrderItems_LampSlots] FOREIGN KEY ([SlotId]) REFERENCES [LampSlots]([SlotId])
    );

    -- 索引
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems]([OrderId]);
    CREATE INDEX [IX_OrderItems_SlotId] ON [OrderItems]([SlotId]);
END
GO

-- =====================================================
-- 付款資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Payments] (
        [PaymentId] INT IDENTITY(1,1) PRIMARY KEY,
        [OrderId] INT NOT NULL UNIQUE,
        [PaymentMethod] NVARCHAR(20) NOT NULL, -- CASH, CARD, TRANSFER
        [AmountDue] DECIMAL(10,2) NOT NULL,
        [AmountReceived] DECIMAL(10,2) NOT NULL,
        [ChangeAmount] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [ReceivedByWorkstation] NVARCHAR(50) NOT NULL,
        [Notes] NVARCHAR(200) NULL,
        [PaymentTime] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderId]) REFERENCES [Orders]([OrderId])
    );

    -- 索引
    CREATE INDEX [IX_Payments_OrderId] ON [Payments]([OrderId]);
END
GO

-- =====================================================
-- 稽核紀錄資料表
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [AuditLogId] INT IDENTITY(1,1) PRIMARY KEY,
        [Action] NVARCHAR(50) NOT NULL,
        [EntityType] NVARCHAR(50) NOT NULL,
        [EntityId] INT NOT NULL,
        [WorkstationId] NVARCHAR(50) NOT NULL,
        [OldValue] NVARCHAR(MAX) NULL,
        [NewValue] NVARCHAR(MAX) NULL,
        [Details] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- 索引
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs]([EntityType], [EntityId]);
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs]([CreatedAt]);
    CREATE INDEX [IX_AuditLogs_WorkstationId] ON [AuditLogs]([WorkstationId]);
END
GO

PRINT N'Schema 建立完成（如果不存在）';
GO
