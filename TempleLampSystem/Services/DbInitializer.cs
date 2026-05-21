using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // 資料庫結構由 DbMigrationService 負責建立，這裡只做資料初始化

        // 初始化燈種
        InitializeLamps(context);

        // 確保既有燈種的宮廟別正確（每次啟動都檢查）
        UpdateLampTemples(context);

        // 確保所有客戶都有編號
        AssignCustomerCodes(context);
    }

    private static void InitializeLamps(AppDbContext context)
    {
        // 不論是否已有資料，統一交給 SyncLampsFromConfig 處理
    }

    // 每次啟動時，將資料庫燈種與 appsettings.json 同步
    private static void UpdateLampTemples(AppDbContext context)
    {
        var configs = AppSettings.Instance.Lamps;
        if (configs.Count == 0)
            return;

        var dbLamps = context.Lamps.ToList();
        var dbByCode = dbLamps.ToDictionary(l => l.LampCode);
        var updated = false;

        // 新增或更新 appsettings.json 中有 LampName 的燈種
        foreach (var cfg in configs)
        {
            if (string.IsNullOrWhiteSpace(cfg.LampName))
                continue;

            if (dbByCode.TryGetValue(cfg.LampCode, out var existing))
            {
                // 更新欄位
                if (existing.LampName != cfg.LampName) { existing.LampName = cfg.LampName; updated = true; }
                if (existing.Temple != cfg.Temple)     { existing.Temple = cfg.Temple;       updated = true; }
                if (existing.Deity != cfg.Deity)       { existing.Deity = cfg.Deity;         updated = true; }
                if (existing.MaxQuota != cfg.MaxQuota) { existing.MaxQuota = cfg.MaxQuota;   updated = true; }
            }
            else
            {
                // 新燈種
                context.Lamps.Add(new Lamp
                {
                    LampCode = cfg.LampCode,
                    LampName = cfg.LampName,
                    Temple   = cfg.Temple,
                    Deity    = cfg.Deity,
                    MaxQuota = cfg.MaxQuota,
                });
                updated = true;
            }
        }

        // 移除 appsettings.json 中 LampName 為空白（或不存在）的燈種
        // 若該燈種已有點燈紀錄則保留，避免破壞歷史資料
        var configCodes = configs
            .Where(c => !string.IsNullOrWhiteSpace(c.LampName))
            .Select(c => c.LampCode)
            .ToHashSet();

        foreach (var lamp in dbLamps)
        {
            if (configCodes.Contains(lamp.LampCode))
                continue;

            var hasOrders = context.LampOrders.Any(o => o.LampId == lamp.Id);
            if (!hasOrders)
            {
                context.Lamps.Remove(lamp);
                updated = true;
            }
        }

        if (updated)
            context.SaveChanges();
    }

    /// <summary>
    /// 為缺少編號的客戶自動產生 6 碼流水編號
    /// </summary>
    internal static void AssignCustomerCodes(AppDbContext context)
    {
        var customersWithoutCode = context.Customers
            .Where(c => c.CustomerCode == null || c.CustomerCode == "")
            .OrderBy(c => c.UpdatedAt)
            .ToList();

        if (customersWithoutCode.Count == 0)
            return;

        // 取得目前最大編號
        var maxCode = context.Customers
            .Where(c => c.CustomerCode != null && c.CustomerCode != "")
            .Select(c => c.CustomerCode)
            .ToList()
            .Select(code => int.TryParse(code, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        var nextCode = maxCode + 1;
        foreach (var customer in customersWithoutCode)
        {
            customer.CustomerCode = nextCode.ToString("D6");
            nextCode++;
        }

        context.SaveChanges();
    }

}
