using Microsoft.EntityFrameworkCore;

namespace TempleLampSystem.Services;

public static class DbMigrationService
{
    private static readonly Dictionary<string, string> Migrations = new()
    {
        ["1.0.0"] = @"
            -- 初始版本，無需遷移
        ",
        ["1.1.0"] = @"
            CREATE TABLE IF NOT EXISTS ""SyncQueue"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""EntityType"" INTEGER NOT NULL,
                ""EntityId"" TEXT NOT NULL,
                ""Operation"" INTEGER NOT NULL,
                ""JsonData"" TEXT,
                ""CreatedAt"" TEXT NOT NULL,
                ""RetryCount"" INTEGER NOT NULL DEFAULT 0,
                ""LastError"" TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_syncqueue_created ON ""SyncQueue""(""CreatedAt"");

            CREATE TABLE IF NOT EXISTS ""SyncConflicts"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""EntityType"" INTEGER NOT NULL,
                ""EntityId"" TEXT NOT NULL,
                ""LocalData"" TEXT,
                ""RemoteData"" TEXT,
                ""LocalUpdatedAt"" TEXT NOT NULL,
                ""RemoteUpdatedAt"" TEXT NOT NULL,
                ""DetectedAt"" TEXT NOT NULL,
                ""Resolution"" INTEGER,
                ""ResolvedAt"" TEXT
            );
        ",
        ["1.2.0"] = @"
            ALTER TABLE ""Customers"" ADD COLUMN ""Village"" TEXT;
            ALTER TABLE ""Customers"" ADD COLUMN ""PostalCode"" TEXT;
            ALTER TABLE ""Customers"" ADD COLUMN ""BirthYear"" INTEGER;
            ALTER TABLE ""Customers"" ADD COLUMN ""BirthMonth"" INTEGER;
            ALTER TABLE ""Customers"" ADD COLUMN ""BirthDay"" INTEGER;
            ALTER TABLE ""Customers"" ADD COLUMN ""BirthHour"" TEXT;
        ",
        ["1.3.0"] = @"
            DELETE FROM ""LampOrders"" WHERE ""LampId"" IN (SELECT ""Id"" FROM ""Lamps"" WHERE ""LampCode"" IN ('CAISHEN','WENCHANG','YUANGUANG','YAOSHIPFO'));
            DELETE FROM ""Lamps"" WHERE ""LampCode"" IN ('CAISHEN','WENCHANG','YUANGUANG','YAOSHIPFO');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('TAISUI', '太歲燈');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('PINGAN', '平安燈');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('GUANGMING', '光明燈');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('YOUXIANG', '油香');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('YOUXIANG_WU', '油香(無)');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('YOUXIANG_JN', '油香急難救');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('YOUXIANG_FD', '油香福德祠');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('FACAI', '發財燈');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('SHENGPING', '聖平');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('SHENGGUANG', '聖光');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('SHENGYOU', '聖油');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('KAOSHANG', '犒賞會');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('FUYOU', '福油');
            INSERT OR IGNORE INTO ""Lamps"" (""LampCode"", ""LampName"") VALUES ('HEJIA_PINGAN', '闔家平安燈');
        ",
        ["1.3.1"] = @"
            DELETE FROM ""LampOrders"" WHERE ""LampId"" IN (SELECT ""Id"" FROM ""Lamps"" WHERE ""LampCode"" IN ('CAISHEN','WENCHANG','YUANGUANG','YAOSHIPFO'));
            DELETE FROM ""Lamps"" WHERE ""LampCode"" IN ('CAISHEN','WENCHANG','YUANGUANG','YAOSHIPFO');
        ",
        ["1.4.0"] = @"
            ALTER TABLE ""Lamps"" ADD COLUMN ""Temple"" TEXT;
            ALTER TABLE ""Lamps"" ADD COLUMN ""Deity"" TEXT;
        ",
        ["1.5.0"] = @"
            -- 已被 1.6.0 取代
        ",
        ["1.6.0"] = @"
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'TAISUI';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'GUANGMING';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'YOUXIANG';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'YOUXIANG_WU';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'HEJIA_PINGAN';
            UPDATE ""Lamps"" SET ""Temple"" = '福德祠', ""Deity"" = '福德正神' WHERE ""LampCode"" = 'YOUXIANG_FD';
            UPDATE ""Lamps"" SET ""Temple"" = '福德祠', ""Deity"" = '福德正神' WHERE ""LampCode"" = 'FACAI';
            UPDATE ""Lamps"" SET ""Temple"" = '聖雲宮', ""Deity"" = '保生大帝' WHERE ""LampCode"" = 'SHENGGUANG';
            UPDATE ""Lamps"" SET ""Temple"" = '聖雲宮', ""Deity"" = '保生大帝' WHERE ""LampCode"" = 'SHENGYOU';
        ",
        ["1.7.0"] = @"
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'TAISUI';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'GUANGMING';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'YOUXIANG';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'YOUXIANG_WU';
            UPDATE ""Lamps"" SET ""Temple"" = '鳳屏宮', ""Deity"" = '神農大帝' WHERE ""LampCode"" = 'HEJIA_PINGAN';
            UPDATE ""Lamps"" SET ""Temple"" = '福德祠', ""Deity"" = '福德正神' WHERE ""LampCode"" = 'YOUXIANG_FD';
            UPDATE ""Lamps"" SET ""Temple"" = '福德祠', ""Deity"" = '福德正神' WHERE ""LampCode"" = 'FACAI';
            UPDATE ""Lamps"" SET ""Temple"" = '聖雲宮', ""Deity"" = '保生大帝' WHERE ""LampCode"" = 'SHENGGUANG';
            UPDATE ""Lamps"" SET ""Temple"" = '聖雲宮', ""Deity"" = '保生大帝' WHERE ""LampCode"" = 'SHENGYOU';
        ",
        ["1.8.0"] = @"
            ALTER TABLE ""Customers"" ADD COLUMN ""CustomerCode"" TEXT;
            CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_code ON ""Customers""(""CustomerCode"");
        ",
        ["1.9.0"] = @"
            ALTER TABLE ""Lamps"" ADD COLUMN ""MaxQuota"" INTEGER NOT NULL DEFAULT 0;
        ",
        ["1.10.0"] = @"
            ALTER TABLE ""LampOrders"" ADD COLUMN ""Note"" TEXT;
        "
    };

    public static void ApplyMigrations(AppDbContext context)
    {
        // 確保基本結構存在
        context.Database.EnsureCreated();

        try
        {
            // 建立版本表
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""_DbVersion"" (
                    ""Version"" TEXT PRIMARY KEY,
                    ""AppliedAt"" TEXT NOT NULL
                )
            ");

            // 檢查並套用遷移
            foreach (var migration in Migrations.OrderBy(m => m.Key))
            {
                var countResult = context.Database
                    .SqlQueryRaw<int>($"SELECT COUNT(*) as Value FROM \"_DbVersion\" WHERE \"Version\" = '{migration.Key}'")
                    .ToList();

                var applied = countResult.FirstOrDefault();

                if (applied == 0)
                {
                    // 執行遷移（跳過空白或註解）
                    var sql = migration.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(sql) && !sql.StartsWith("--"))
                    {
                        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var trimmed = statement.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("--"))
                            {
                                try
                                {
                                    context.Database.ExecuteSqlRaw(trimmed);
                                }
                                catch
                                {
                                    // 忽略已存在的表/索引錯誤
                                }
                            }
                        }
                    }

                    context.Database.ExecuteSqlRaw(
                        $"INSERT OR IGNORE INTO \"_DbVersion\" (\"Version\", \"AppliedAt\") VALUES ('{migration.Key}', datetime('now'))");
                }
            }
        }
        catch
        {
            // 遷移失敗時靜默處理，不影響應用程式啟動
        }
    }
}
