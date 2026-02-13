using System.Globalization;
using System.IO;
using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class DataImportService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseService _supabaseService;

    public DataImportService(AppDbContext context, ISupabaseService supabaseService)
    {
        _context = context;
        _supabaseService = supabaseService;
    }

    public class ImportProgress
    {
        public int TotalCustomers { get; set; }
        public int ImportedCustomers { get; set; }
        public int TotalOrders { get; set; }
        public int ImportedOrders { get; set; }
        public int SkippedOrders { get; set; }
        public string CurrentStep { get; set; } = "";
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 從 CSV 匯入舊系統資料
    /// </summary>
    public async Task<ImportProgress> ImportAsync(
        string customersCsvPath,
        string lampOrdersCsvPath,
        IProgress<ImportProgress>? progress = null)
    {
        var result = new ImportProgress();

        // 1. 讀取 CSV
        result.CurrentStep = "讀取客戶 CSV...";
        progress?.Report(result);
        var customerRows = await ReadCsvAsync(customersCsvPath);
        result.TotalCustomers = customerRows.Count;

        result.CurrentStep = "讀取點燈 CSV...";
        progress?.Report(result);
        var orderRows = await ReadCsvAsync(lampOrdersCsvPath);
        result.TotalOrders = orderRows.Count;

        // 2. 載入燈種對照表（LampName → Lamp.Id）
        var lamps = await _context.Lamps.AsNoTracking().ToListAsync();
        var lampNameToId = lamps.ToDictionary(l => l.LampName, l => l.Id);

        // 舊系統燈種名稱可能有些微差異，加入別名
        if (!lampNameToId.ContainsKey("油香急難救助") && lampNameToId.ContainsKey("油香急難救"))
            lampNameToId["油香急難救助"] = lampNameToId["油香急難救"];

        // 3. 清除現有測試資料
        result.CurrentStep = "清除現有測試資料...";
        progress?.Report(result);
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM LampOrders");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Customers");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM SyncQueue");
        _context.ChangeTracker.Clear();

        // 4. 匯入客戶（CusNo → Guid 對照表）
        result.CurrentStep = "匯入客戶資料...";
        progress?.Report(result);
        var cusNoToGuid = new Dictionary<string, Guid>();
        var customerBatch = new List<Customer>();

        foreach (var row in customerRows)
        {
            try
            {
                var cusNo = GetField(row, "CusNo").PadLeft(6, '0');
                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = cusNo,
                    Name = GetField(row, "CusName"),
                    Phone = NullIfEmpty(GetField(row, "CusTel")),
                    Mobile = NullIfEmpty(GetField(row, "CusTel")),
                    Address = NullIfEmpty(CleanAddress(GetField(row, "CusAddr"))),
                    Village = NullIfEmpty(GetField(row, "CusZone")),
                    PostalCode = NullIfEmpty(GetField(row, "Zip")),
                    Note = NullIfEmpty(GetField(row, "Mark")),
                    BirthYear = ParseBirthInt(GetField(row, "YY")),
                    BirthMonth = ParseBirthInt(GetField(row, "MM")),
                    BirthDay = ParseBirthInt(GetField(row, "DD")),
                    BirthHour = ConvertBirthHour(GetField(row, "HH")),
                    UpdatedAt = DateTime.Now
                };

                if (string.IsNullOrEmpty(customer.Name))
                    continue;

                cusNoToGuid[cusNo] = customer.Id;
                customerBatch.Add(customer);

                if (customerBatch.Count >= 500)
                {
                    await _context.Customers.AddRangeAsync(customerBatch);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                    result.ImportedCustomers += customerBatch.Count;
                    result.CurrentStep = $"匯入客戶 {result.ImportedCustomers}/{result.TotalCustomers}...";
                    progress?.Report(result);
                    customerBatch.Clear();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"客戶匯入錯誤 (row {result.ImportedCustomers}): {ex.Message}");
            }
        }

        if (customerBatch.Count > 0)
        {
            await _context.Customers.AddRangeAsync(customerBatch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            result.ImportedCustomers += customerBatch.Count;
        }

        // 5. 匯入點燈紀錄
        result.CurrentStep = "匯入點燈紀錄...";
        progress?.Report(result);
        var orderBatch = new List<LampOrder>();

        foreach (var row in orderRows)
        {
            try
            {
                var lampType = GetField(row, "LampType");
                var cusNo = GetField(row, "CusNo").PadLeft(6, '0');

                if (!lampNameToId.TryGetValue(lampType, out var lampId))
                {
                    result.SkippedOrders++;
                    continue;
                }

                if (!cusNoToGuid.TryGetValue(cusNo, out var customerId))
                {
                    result.SkippedOrders++;
                    continue;
                }

                var startDate = ParseRocDate(GetField(row, "Date0"));
                var endDate = ParseRocDate(GetField(row, "Date1"));

                if (startDate == null || endDate == null)
                {
                    result.SkippedOrders++;
                    continue;
                }

                var order = new LampOrder
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    LampId = lampId,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    Year = startDate.Value.Year,
                    Price = ParseDecimal(GetField(row, "Fare")),
                    Note = NullIfEmpty(GetField(row, "Mark")),
                    CreatedAt = startDate.Value,
                    UpdatedAt = DateTime.Now
                };

                orderBatch.Add(order);

                if (orderBatch.Count >= 500)
                {
                    await _context.LampOrders.AddRangeAsync(orderBatch);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                    result.ImportedOrders += orderBatch.Count;
                    result.CurrentStep = $"匯入點燈 {result.ImportedOrders}/{result.TotalOrders}...";
                    progress?.Report(result);
                    orderBatch.Clear();
                }
            }
            catch (Exception ex)
            {
                result.SkippedOrders++;
                result.Errors.Add($"點燈匯入錯誤 (row {result.ImportedOrders}): {ex.Message}");
            }
        }

        if (orderBatch.Count > 0)
        {
            await _context.LampOrders.AddRangeAsync(orderBatch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            result.ImportedOrders += orderBatch.Count;
        }

        // 6. 上傳到 Supabase
        if (_supabaseService.IsConfigured)
        {
            result.CurrentStep = "上傳到雲端（這可能需要幾分鐘）...";
            progress?.Report(result);

            try
            {
                // 先上傳燈種
                foreach (var lamp in lamps)
                {
                    try { await _supabaseService.UpsertLampAsync(lamp); } catch { }
                }

                // 分批上傳客戶
                var allCustomers = await _context.Customers.AsNoTracking().ToListAsync();
                var uploaded = 0;
                foreach (var c in allCustomers)
                {
                    try
                    {
                        await _supabaseService.UpsertCustomerAsync(c);
                        uploaded++;
                        if (uploaded % 200 == 0)
                        {
                            result.CurrentStep = $"上傳客戶到雲端 {uploaded}/{allCustomers.Count}...";
                            progress?.Report(result);
                        }
                    }
                    catch { }
                }

                // 分批上傳點燈紀錄
                var allOrders = await _context.LampOrders.AsNoTracking().ToListAsync();
                uploaded = 0;
                foreach (var o in allOrders)
                {
                    try
                    {
                        await _supabaseService.UpsertLampOrderAsync(o);
                        uploaded++;
                        if (uploaded % 200 == 0)
                        {
                            result.CurrentStep = $"上傳點燈到雲端 {uploaded}/{allOrders.Count}...";
                            progress?.Report(result);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"雲端上傳錯誤：{ex.Message}");
            }
        }

        result.CurrentStep = "匯入完成！";
        progress?.Report(result);
        return result;
    }

    #region CSV 解析

    private static async Task<List<Dictionary<string, string>>> ReadCsvAsync(string path)
    {
        var rows = new List<Dictionary<string, string>>();
        var lines = await File.ReadAllLinesAsync(path, System.Text.Encoding.UTF8);

        if (lines.Length == 0) return rows;

        var headers = ParseCsvLine(lines[0]);

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>();

            for (var j = 0; j < headers.Length && j < values.Length; j++)
            {
                row[headers[j]] = values[j];
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var field = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return fields.ToArray();
    }

    #endregion

    #region 資料轉換

    private static string GetField(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value.Trim() : "";
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 清理地址（舊系統有些地址開頭重複了郵遞區號）
    /// </summary>
    private static string CleanAddress(string addr)
    {
        if (string.IsNullOrEmpty(addr)) return addr;

        // 移除開頭的3碼郵遞區號（如 "811高雄市..." → "高雄市..."）
        if (addr.Length > 3 && int.TryParse(addr[..3], out _) && !char.IsDigit(addr[3]))
        {
            return addr[3..];
        }

        return addr;
    }

    /// <summary>
    /// 解析出生年月日，0 表示「吉」
    /// </summary>
    private static int? ParseBirthInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var n)) return n;
        return null;
    }

    /// <summary>
    /// 轉換出生時辰：舊系統 "吉" → 新系統 "吉時"，"子" → "子時"
    /// </summary>
    private static string? ConvertBirthHour(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.EndsWith("時")) return v;
        return v + "時";
    }

    /// <summary>
    /// 解析民國日期字串 "110/01/01" → DateTime(2021, 1, 1)
    /// </summary>
    private static DateTime? ParseRocDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Split('/');
        if (parts.Length != 3) return null;

        if (int.TryParse(parts[0], out var rocYear) &&
            int.TryParse(parts[1], out var month) &&
            int.TryParse(parts[2], out var day))
        {
            var westernYear = rocYear + 1911;
            if (westernYear > 1900 && westernYear < 2100 && month >= 1 && month <= 12 && day >= 1 && day <= 31)
            {
                try { return new DateTime(westernYear, month, day); }
                catch { return null; }
            }
        }

        return null;
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return 0;
    }

    #endregion
}
