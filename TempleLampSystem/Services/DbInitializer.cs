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

        // 測試客戶資料
        var customers = new List<Customer>
        {
            new() { Id = Guid.NewGuid(), Name = "王大明", Phone = "02-2345-6789", Mobile = "0912-345-678", Address = "台北市中正區忠孝東路一段100號", Note = "老客戶", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "李美玲", Phone = "02-8765-4321", Mobile = "0923-456-789", Address = "台北市大安區信義路三段50號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "張志豪", Phone = "03-1234-5678", Mobile = "0934-567-890", Address = "桃園市中壢區中山路200號", Note = "每年都來點燈", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "陳淑芬", Phone = "04-2233-4455", Mobile = "0945-678-901", Address = "台中市西區台灣大道二段300號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "林建國", Phone = "02-5566-7788", Mobile = "0956-789-012", Address = "新北市板橋區文化路一段150號", Note = "VIP客戶", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "黃雅琪", Phone = "07-9988-7766", Mobile = "0967-890-123", Address = "高雄市前鎮區中山二路500號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "吳宗翰", Phone = "06-5544-3322", Mobile = "0978-901-234", Address = "台南市東區東門路二段80號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "劉怡君", Phone = "02-1122-3344", Mobile = "0989-012-345", Address = "台北市松山區民生東路五段60號", Note = "介紹很多朋友", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "許文龍", Phone = "03-6677-8899", Mobile = "0911-223-344", Address = "新竹市東區光復路一段250號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "蔡佳慧", Phone = "04-7788-9900", Mobile = "0922-334-455", Address = "台中市北區學士路400號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "楊明哲", Phone = "02-3344-5566", Mobile = "0933-445-566", Address = "新北市新店區北新路三段70號", Note = "", UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = "周美華", Phone = "05-2233-1100", Mobile = "0944-556-677", Address = "嘉義市西區中山路100號", Note = "每年固定點光明燈", UpdatedAt = now },
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
