using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // 確保資料庫已建立
        context.Database.EnsureCreated();

        // 如果已有燈種資料，則跳過
        if (context.Lamps.Any())
            return;

        // 預設燈種
        var lamps = new List<Lamp>
        {
            new() { LampCode = "TAISUI",    LampName = "太歲燈" },
            new() { LampCode = "GUANGMING", LampName = "光明燈" },
            new() { LampCode = "PINGAN",    LampName = "平安燈" },
            new() { LampCode = "CAISHEN",   LampName = "財神燈" },
            new() { LampCode = "WENCHANG",  LampName = "文昌燈" },
            new() { LampCode = "YUANGUANG", LampName = "元辰燈" },
            new() { LampCode = "YAOSHIPFO", LampName = "藥師燈" },
        };

        context.Lamps.AddRange(lamps);
        context.SaveChanges();
    }
}
