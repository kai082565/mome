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
        if (context.Lamps.Any())
            return;

        // MaxQuota = 0 表示不限量
        var lamps = new List<Lamp>
        {
            new() { LampCode = "TAISUI",       LampName = "太歲燈",     Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 4880 },
            new() { LampCode = "PINGAN",       LampName = "平安燈",     Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 0 },
            new() { LampCode = "GUANGMING",    LampName = "光明燈",     Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 3000 },
            new() { LampCode = "YOUXIANG",     LampName = "油香",       Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 0 },
            new() { LampCode = "YOUXIANG_WU",  LampName = "油香(無)",   Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 0 },
            new() { LampCode = "YOUXIANG_JN",  LampName = "油香急難救", Temple = "聖雲宮", Deity = "保生大帝", MaxQuota = 0 },
            new() { LampCode = "YOUXIANG_FD",  LampName = "油香福德祠", Temple = "福德祠", Deity = "福德正神", MaxQuota = 0 },
            new() { LampCode = "FACAI",        LampName = "發財燈",     Temple = "福德祠", Deity = "福德正神", MaxQuota = 0 },
            new() { LampCode = "SHENGPING",    LampName = "聖平",       Temple = "聖雲宮", Deity = "保生大帝", MaxQuota = 0 },
            new() { LampCode = "SHENGGUANG",   LampName = "聖光",       Temple = "聖雲宮", Deity = "保生大帝", MaxQuota = 3000 },
            new() { LampCode = "SHENGYOU",     LampName = "聖油",       Temple = "聖雲宮", Deity = "保生大帝", MaxQuota = 0 },
            new() { LampCode = "KAOSHANG",     LampName = "犒賞會",     Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 0 },
            new() { LampCode = "FUYOU",        LampName = "福油",       Temple = "福德祠", Deity = "福德正神", MaxQuota = 0 },
            new() { LampCode = "HEJIA_PINGAN", LampName = "闔家平安燈", Temple = "鳳屏宮", Deity = "神農大帝", MaxQuota = 70 },
        };

        context.Lamps.AddRange(lamps);
        context.SaveChanges();
    }

    /// <summary>
    /// 燈種固定對照表：宮廟別、神明別、年度限量
    /// </summary>
    private static readonly Dictionary<string, (string? Temple, string? Deity, int MaxQuota)> LampConfigMap = new()
    {
        { "TAISUI",       ("鳳屏宮", "神農大帝", 4880) },  // 太歲燈
        { "PINGAN",       ("鳳屏宮", "神農大帝", 0) },     // 平安燈
        { "GUANGMING",    ("鳳屏宮", "神農大帝", 3000) },  // 光明燈
        { "YOUXIANG",     ("鳳屏宮", "神農大帝", 0) },     // 油香
        { "YOUXIANG_WU",  ("鳳屏宮", "神農大帝", 0) },     // 油香(無)
        { "YOUXIANG_JN",  ("聖雲宮", "保生大帝", 0) },     // 油香急難救
        { "YOUXIANG_FD",  ("福德祠", "福德正神", 0) },     // 油香福德祠
        { "FACAI",        ("福德祠", "福德正神", 0) },     // 發財燈
        { "SHENGPING",    ("聖雲宮", "保生大帝", 0) },     // 聖平
        { "SHENGGUANG",   ("聖雲宮", "保生大帝", 3000) },  // 聖光
        { "SHENGYOU",     ("聖雲宮", "保生大帝", 0) },     // 聖油
        { "KAOSHANG",     ("鳳屏宮", "神農大帝", 0) },     // 犒賞會
        { "FUYOU",        ("福德祠", "福德正神", 0) },     // 福油
        { "HEJIA_PINGAN", ("鳳屏宮", "神農大帝", 70) },    // 闔家平安燈
    };

    /// <summary>
    /// 更新既有燈種的宮廟別、神明別、限量（每次啟動都執行，確保資料正確）
    /// </summary>
    private static void UpdateLampTemples(AppDbContext context)
    {
        var lamps = context.Lamps.ToList();
        var updated = false;

        foreach (var lamp in lamps)
        {
            if (LampConfigMap.TryGetValue(lamp.LampCode, out var config))
            {
                if (config.Temple != null && lamp.Temple != config.Temple)
                {
                    lamp.Temple = config.Temple;
                    updated = true;
                }
                if (config.Deity != null && lamp.Deity != config.Deity)
                {
                    lamp.Deity = config.Deity;
                    updated = true;
                }
                if (lamp.MaxQuota != config.MaxQuota)
                {
                    lamp.MaxQuota = config.MaxQuota;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            context.SaveChanges();
        }
    }

    /// <summary>
    /// 為缺少編號的客戶自動產生 6 碼流水編號
    /// </summary>
    private static void AssignCustomerCodes(AppDbContext context)
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
