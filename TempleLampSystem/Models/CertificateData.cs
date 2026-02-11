using TempleLampSystem.Helpers;

namespace TempleLampSystem.Models;

/// <summary>
/// 感謝狀列印資料模型
/// </summary>
public class CertificateData
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BirthYear { get; set; }
    public string? BirthMonth { get; set; }
    public string? BirthDay { get; set; }
    public string LunarStartYear { get; set; } = string.Empty;
    public string LunarStartMonth { get; set; } = string.Empty;
    public string LunarStartDay { get; set; } = string.Empty;
    public string? LunarStartHour { get; set; }
    public string LunarEndYear { get; set; } = string.Empty;
    public string LunarEndMonth { get; set; } = string.Empty;
    public string LunarEndDay { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string? LampType { get; set; }

    /// <summary>
    /// 從單一客戶建立感謝狀資料
    /// </summary>
    public static CertificateData FromOrder(LampOrder order, Customer customer, Lamp lamp)
    {
        var now = DateTime.Now;
        var (startYear, startMonth, startDay, _) = LunarCalendarHelper.GetLunarDate(now);
        var endDate = LunarCalendarHelper.GetLunarYearEndDate(now);
        var (endYear, endMonth, endDay, _) = LunarCalendarHelper.GetLunarDate(endDate);

        // 取得當前時辰
        var hour = GetChineseHour(now);

        // 完整地址（郵遞區號 + 里 + 地址）
        var fullAddress = string.Join("", new[]
        {
            customer.PostalCode,
            customer.Village,
            customer.Address
        }.Where(s => !string.IsNullOrEmpty(s)));

        return new CertificateData
        {
            Name = customer.Name,
            Phone = customer.Phone ?? customer.Mobile,
            Address = string.IsNullOrEmpty(fullAddress) ? null : fullAddress,
            BirthYear = customer.BirthYear?.ToString(),
            BirthMonth = customer.BirthMonth?.ToString(),
            BirthDay = customer.BirthDay?.ToString(),
            LunarStartYear = startYear.ToString(),
            LunarStartMonth = startMonth.ToString(),
            LunarStartDay = startDay.ToString(),
            LunarStartHour = hour,
            LunarEndYear = endYear.ToString(),
            LunarEndMonth = "12",
            LunarEndDay = "24",
            Amount = order.Price.ToString("N0"),
            LampType = lamp.LampName
        };
    }

    /// <summary>
    /// 闔家平安燈：多位客戶合併名字，只印一張
    /// </summary>
    public static CertificateData FromFamilyOrder(List<(Customer Customer, LampOrder Order)> customerOrders, Lamp lamp)
    {
        if (customerOrders.Count == 0)
            throw new ArgumentException("至少需要一位客戶");

        var firstCustomer = customerOrders[0].Customer;
        var firstOrder = customerOrders[0].Order;

        // 合併所有客戶名字（用頓號分隔）
        var allNames = string.Join("、", customerOrders.Select(co => co.Customer.Name));

        var now = DateTime.Now;
        var (startYear, startMonth, startDay, _) = LunarCalendarHelper.GetLunarDate(now);
        var endDate = LunarCalendarHelper.GetLunarYearEndDate(now);
        var (endYear, endMonth, endDay, _) = LunarCalendarHelper.GetLunarDate(endDate);

        var hour = GetChineseHour(now);

        // 使用第一位客戶的電話和地址
        var fullAddress = string.Join("", new[]
        {
            firstCustomer.PostalCode,
            firstCustomer.Village,
            firstCustomer.Address
        }.Where(s => !string.IsNullOrEmpty(s)));

        // 闔家平安燈固定金額，使用第一筆訂單的金額
        var totalAmount = firstOrder.Price;

        return new CertificateData
        {
            Name = allNames,
            Phone = firstCustomer.Phone ?? firstCustomer.Mobile,
            Address = string.IsNullOrEmpty(fullAddress) ? null : fullAddress,
            BirthYear = firstCustomer.BirthYear?.ToString(),
            BirthMonth = firstCustomer.BirthMonth?.ToString(),
            BirthDay = firstCustomer.BirthDay?.ToString(),
            LunarStartYear = startYear.ToString(),
            LunarStartMonth = startMonth.ToString(),
            LunarStartDay = startDay.ToString(),
            LunarStartHour = hour,
            LunarEndYear = endYear.ToString(),
            LunarEndMonth = "12",
            LunarEndDay = "24",
            Amount = totalAmount.ToString("N0"),
            LampType = lamp.LampName
        };
    }

    /// <summary>
    /// 根據小時取得中文時辰
    /// </summary>
    private static string GetChineseHour(DateTime time)
    {
        string[] hours = ["子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"];
        int index = ((time.Hour + 1) % 24) / 2;
        return hours[index] + "時";
    }
}
