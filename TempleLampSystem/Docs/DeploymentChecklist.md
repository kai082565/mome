# 上線檢查清單

## 一、環境準備

### 1.1 Supabase 設定
- [ ] 建立 Supabase 專案
- [ ] 執行資料表建立 SQL
- [ ] 取得 Project URL
- [ ] 取得 Anon Key
- [ ] 設定 Row Level Security

### 1.2 本地環境
- [ ] 安裝 .NET 8 SDK
- [ ] 安裝 Visual Studio 或 VS Code

---

## 二、應用程式設定

### 2.1 設定檔 (appsettings.json)
- [ ] 設定 Supabase URL
- [ ] 設定 Supabase Anon Key
- [ ] 設定宮廟名稱、地址、電話

---

## 三、功能測試

### 3.1 基本功能
- [ ] 新增客戶
- [ ] 搜尋客戶（電話/手機）
- [ ] 點燈
- [ ] 重複點燈檢查
- [ ] 同電話檢查
- [ ] 即將到期提醒

### 3.2 列印功能
- [ ] 預覽單據
- [ ] 直接列印
- [ ] 儲存 PDF

### 3.3 同步功能
- [ ] 上傳至雲端
- [ ] 從雲端下載
- [ ] 離線操作
- [ ] 回線自動同步

---

## 四、部署步驟

### 4.1 建置發佈版本
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 4.2 建立安裝程式
1. 開啟 Inno Setup Compiler
2. 載入 `setup.iss`
3. 編譯產生安裝程式

### 4.3 測試安裝程式
- [ ] 在乾淨的測試機安裝
- [ ] 確認所有功能正常

---

## 五、Supabase SQL

```sql
-- Customers
CREATE TABLE "Customers" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" VARCHAR(100) NOT NULL,
    "Phone" VARCHAR(20),
    "Mobile" VARCHAR(20),
    "Address" VARCHAR(500),
    "Note" VARCHAR(1000),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Lamps
CREATE TABLE "Lamps" (
    "Id" SERIAL PRIMARY KEY,
    "LampCode" VARCHAR(50) NOT NULL UNIQUE,
    "LampName" VARCHAR(100) NOT NULL
);

-- 預設燈種
INSERT INTO "Lamps" ("LampCode", "LampName") VALUES
    ('TAISUI', '太歲燈'),
    ('GUANGMING', '光明燈'),
    ('PINGAN', '平安燈'),
    ('CAISHEN', '財神燈'),
    ('WENCHANG', '文昌燈');

-- LampOrders
CREATE TABLE "LampOrders" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CustomerId" UUID NOT NULL REFERENCES "Customers"("Id"),
    "LampId" INTEGER NOT NULL REFERENCES "Lamps"("Id"),
    "StartDate" DATE NOT NULL,
    "EndDate" DATE NOT NULL,
    "Year" INTEGER NOT NULL,
    "Price" DECIMAL(10,2) NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_lamporders_year ON "LampOrders"("Year");
CREATE INDEX idx_lamporders_customer ON "LampOrders"("CustomerId");

-- RLS Policies
ALTER TABLE "Customers" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Lamps" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LampOrders" ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Allow all" ON "Customers" FOR ALL USING (true);
CREATE POLICY "Allow all" ON "Lamps" FOR ALL USING (true);
CREATE POLICY "Allow all" ON "LampOrders" FOR ALL USING (true);
```
