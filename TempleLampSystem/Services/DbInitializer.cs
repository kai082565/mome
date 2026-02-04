using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // 確保資料庫已建立
        context.Database.EnsureCreated();

        // 初始化燈種
        InitializeLamps(context);

        // 初始化測試資料（僅在沒有客戶資料時）
        InitializeTestData(context);
    }

    private static void InitializeLamps(AppDbContext context)
    {
        if (context.Lamps.Any())
            return;

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

    private static void InitializeTestData(AppDbContext context)
    {
        // 如果已有客戶資料，則跳過
        if (context.Customers.Any())
            return;

        var random = new Random(42); // 固定種子確保一致性
        var now = DateTime.Now;
        var currentYear = now.Year;

        // 測試客戶資料（包含同一電話多位家庭成員的情況）
        var customers = new List<Customer>
        {
            // 陳家 - 同一支家用電話 07-7654-3210
            new() { Id = Guid.NewGuid(), Name = "陳文雄", Phone = "07-7654-3210", Mobile = "0912-111-222", Address = "高雄市鳳山區鳳屏路100號", Note = "一家之主", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "陳李美惠", Phone = "07-7654-3210", Mobile = "0923-222-333", Address = "高雄市鳳山區鳳屏路100號", Note = "陳文雄之妻", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "陳志明", Phone = "07-7654-3210", Mobile = "0934-333-444", Address = "高雄市鳳山區鳳屏路100號", Note = "陳文雄長子", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "陳雅婷", Phone = "07-7654-3210", Mobile = "", Address = "高雄市鳳山區鳳屏路100號", Note = "陳文雄長女", UpdatedAt = now },

            // 林家 - 同一支家用電話 07-7891-2345
            new() { Id = Guid.NewGuid(), Name = "林國榮", Phone = "07-7891-2345", Mobile = "0945-444-555", Address = "高雄市鳳山區中山西路50號", Note = "老客戶", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "林王秀蘭", Phone = "07-7891-2345", Mobile = "", Address = "高雄市鳳山區中山西路50號", Note = "林國榮之妻", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "林佳蓉", Phone = "07-7891-2345", Mobile = "0956-555-666", Address = "高雄市鳳山區中山西路50號", Note = "林國榮之女", UpdatedAt = now },

            // 王家 - 同一支家用電話 07-7456-7890
            new() { Id = Guid.NewGuid(), Name = "王建宏", Phone = "07-7456-7890", Mobile = "0967-666-777", Address = "高雄市前鎮區中華五路200號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "王張淑娟", Phone = "07-7456-7890", Mobile = "0978-777-888", Address = "高雄市前鎮區中華五路200號", Note = "王建宏之妻", UpdatedAt = now },

            // 黃家 - 同一支家用電話 07-7223-4567
            new() { Id = Guid.NewGuid(), Name = "黃進發", Phone = "07-7223-4567", Mobile = "", Address = "高雄市苓雅區四維三路80號", Note = "每年都來", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "黃陳玉珠", Phone = "07-7223-4567", Mobile = "0989-888-999", Address = "高雄市苓雅區四維三路80號", Note = "黃進發之妻", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "黃俊傑", Phone = "07-7223-4567", Mobile = "0911-999-000", Address = "高雄市苓雅區四維三路80號", Note = "黃進發長子", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "黃美玲", Phone = "07-7223-4567", Mobile = "", Address = "高雄市苓雅區四維三路80號", Note = "黃進發長女", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "黃志豪", Phone = "07-7223-4567", Mobile = "0922-000-111", Address = "高雄市苓雅區四維三路80號", Note = "黃進發次子", UpdatedAt = now },

            // 個人客戶（只有手機）
            new() { Id = Guid.NewGuid(), Name = "張雅琪", Phone = "", Mobile = "0933-111-222", Address = "高雄市鳳山區光復路一段60號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "李宗翰", Phone = "", Mobile = "0944-222-333", Address = "高雄市三民區建工路500號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "吳怡君", Phone = "07-7334-5678", Mobile = "0955-333-444", Address = "高雄市左營區博愛二路100號", Note = "介紹很多朋友", UpdatedAt = now },
        };

        context.Customers.AddRange(customers);
        context.SaveChanges();

        // 取得所有燈種
        var lamps = context.Lamps.ToList();
        var lampOrders = new List<LampOrder>();

        // 金額對照表
        var priceMap = new Dictionary<string, decimal>
        {
            { "太歲燈", 1200 },
            { "光明燈", 600 },
            { "平安燈", 500 },
            { "財神燈", 800 },
            { "文昌燈", 600 },
            { "元辰燈", 1000 },
            { "藥師燈", 800 },
        };

        foreach (var customer in customers)
        {
            // 隨機決定每個客戶有幾個點燈紀錄 (1-4個)
            var orderCount = random.Next(1, 5);
            var usedLamps = new HashSet<int>();

            for (int i = 0; i < orderCount; i++)
            {
                // 隨機選擇一個尚未使用的燈種
                Lamp lamp;
                do
                {
                    lamp = lamps[random.Next(lamps.Count)];
                } while (usedLamps.Contains(lamp.Id) && usedLamps.Count < lamps.Count);

                if (usedLamps.Contains(lamp.Id))
                    continue;

                usedLamps.Add(lamp.Id);

                // 隨機決定點燈年份和狀態
                int year;
                DateTime startDate;
                DateTime endDate;

                var statusType = random.Next(100);
                if (statusType < 50)
                {
                    // 50% - 今年有效的點燈
                    year = currentYear;
                    startDate = new DateTime(currentYear, 1, 1).AddDays(random.Next(0, 60));
                    endDate = startDate.AddYears(1);
                }
                else if (statusType < 75)
                {
                    // 25% - 即將到期（30天內）
                    year = currentYear - 1;
                    startDate = now.AddDays(-365 + random.Next(1, 30));
                    endDate = startDate.AddYears(1);
                }
                else
                {
                    // 25% - 已過期
                    year = currentYear - 1 - random.Next(0, 2);
                    startDate = new DateTime(year, 1, 1).AddDays(random.Next(0, 180));
                    endDate = startDate.AddYears(1);
                }

                var price = priceMap.GetValueOrDefault(lamp.LampName, 600);

                lampOrders.Add(new LampOrder
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    LampId = lamp.Id,
                    Year = year,
                    StartDate = startDate,
                    EndDate = endDate,
                    Price = price,
                    CreatedAt = startDate,
                    UpdatedAt = startDate
                });
            }
        }

        context.LampOrders.AddRange(lampOrders);
        context.SaveChanges();
    }
}
