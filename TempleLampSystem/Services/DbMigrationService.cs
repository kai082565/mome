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
