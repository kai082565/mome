-- =============================================
-- 點燈管理系統 - 資料庫遷移腳本
-- =============================================

-- 版本 1.0.0 初始建立
-- Customers
CREATE TABLE IF NOT EXISTS "Customers" (
    "Id" TEXT PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Phone" TEXT,
    "Mobile" TEXT,
    "Address" TEXT,
    "Note" TEXT,
    "UpdatedAt" TEXT NOT NULL
);

-- Lamps
CREATE TABLE IF NOT EXISTS "Lamps" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "LampCode" TEXT NOT NULL UNIQUE,
    "LampName" TEXT NOT NULL
);

-- LampOrders
CREATE TABLE IF NOT EXISTS "LampOrders" (
    "Id" TEXT PRIMARY KEY,
    "CustomerId" TEXT NOT NULL,
    "LampId" INTEGER NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "Year" INTEGER NOT NULL,
    "Price" REAL NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id"),
    FOREIGN KEY ("LampId") REFERENCES "Lamps"("Id")
);

CREATE INDEX IF NOT EXISTS idx_lamporders_year ON "LampOrders"("Year");
CREATE INDEX IF NOT EXISTS idx_lamporders_customer ON "LampOrders"("CustomerId");

-- 預設燈種
INSERT OR IGNORE INTO "Lamps" ("LampCode", "LampName") VALUES ('TAISUI', '太歲燈');
INSERT OR IGNORE INTO "Lamps" ("LampCode", "LampName") VALUES ('GUANGMING', '光明燈');
INSERT OR IGNORE INTO "Lamps" ("LampCode", "LampName") VALUES ('PINGAN', '平安燈');
INSERT OR IGNORE INTO "Lamps" ("LampCode", "LampName") VALUES ('CAISHEN', '財神燈');
INSERT OR IGNORE INTO "Lamps" ("LampCode", "LampName") VALUES ('WENCHANG', '文昌燈');

-- 版本 1.1.0 新增同步佇列
CREATE TABLE IF NOT EXISTS "SyncQueue" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "EntityType" INTEGER NOT NULL,
    "EntityId" TEXT NOT NULL,
    "Operation" INTEGER NOT NULL,
    "JsonData" TEXT,
    "CreatedAt" TEXT NOT NULL,
    "RetryCount" INTEGER NOT NULL DEFAULT 0,
    "LastError" TEXT
);

CREATE INDEX IF NOT EXISTS idx_syncqueue_created ON "SyncQueue"("CreatedAt");

-- SyncConflicts
CREATE TABLE IF NOT EXISTS "SyncConflicts" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "EntityType" INTEGER NOT NULL,
    "EntityId" TEXT NOT NULL,
    "LocalData" TEXT,
    "RemoteData" TEXT,
    "LocalUpdatedAt" TEXT NOT NULL,
    "RemoteUpdatedAt" TEXT NOT NULL,
    "DetectedAt" TEXT NOT NULL,
    "Resolution" INTEGER,
    "ResolvedAt" TEXT
);

-- 版本記錄表
CREATE TABLE IF NOT EXISTS "_DbVersion" (
    "Version" TEXT PRIMARY KEY,
    "AppliedAt" TEXT NOT NULL
);

INSERT OR IGNORE INTO "_DbVersion" ("Version", "AppliedAt") VALUES ('1.0.0', datetime('now'));
INSERT OR IGNORE INTO "_DbVersion" ("Version", "AppliedAt") VALUES ('1.1.0', datetime('now'));
