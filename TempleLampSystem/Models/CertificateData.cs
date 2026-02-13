namespace TempleLampSystem.Models;

/// <summary>
/// 感謝狀列印資料模型
/// </summary>
public class CertificateData
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BirthYear { get; set; }
    public string? BirthMonth { get; set; }
    public string? BirthDay { get; set; }
    public string? BirthHour { get; set; }
    public string PrintDate { get; set; } = string.Empty;
    public string LunarStartDate { get; set; } = string.Empty;
    public string LunarEndDate { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string? LampType { get; set; }

    /// <summary>
    /// 從單一客戶建立感謝狀資料
    /// </summary>
    public static CertificateData FromOrder(LampOrder order, Customer customer, Lamp lamp)
    {
        var rocYear = Services.AppSettings.Instance.CertificateForm.RocYear;

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
            CustomerCode = customer.CustomerCode,
            Phone = customer.Phone ?? customer.Mobile,
            Address = string.IsNullOrEmpty(fullAddress) ? null : fullAddress,
            BirthYear = FormatBirthField(customer.BirthYear),
            BirthMonth = FormatBirthField(customer.BirthMonth),
            BirthDay = FormatBirthField(customer.BirthDay),
            BirthHour = customer.BirthHour,
            PrintDate = DateTime.Now.ToString("yyyy/MM/dd"),
            LunarStartDate = $"{rocYear}/01/15",
            LunarEndDate = $"{rocYear}/12/24",
            Amount = $"${order.Price:N0}元整",
            LampType = lamp.LampName
        };
    }

    /// <summary>
    /// 0 顯示「吉」，其他顯示數字
    /// </summary>
    private static string? FormatBirthField(int? value)
    {
        if (value == null) return null;
        return value == 0 ? "吉" : value.ToString();
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
        var rocYear = Services.AppSettings.Instance.CertificateForm.RocYear;

        // 合併所有客戶名字（用頓號分隔）
        var allNames = string.Join("、", customerOrders.Select(co => co.Customer.Name));

        // 使用第一位客戶的電話和地址
        var fullAddress = string.Join("", new[]
        {
            firstCustomer.PostalCode,
            firstCustomer.Village,
            firstCustomer.Address
        }.Where(s => !string.IsNullOrEmpty(s)));

        return new CertificateData
        {
            Name = allNames,
            CustomerCode = firstCustomer.CustomerCode,
            Phone = firstCustomer.Phone ?? firstCustomer.Mobile,
            Address = string.IsNullOrEmpty(fullAddress) ? null : fullAddress,
            BirthYear = FormatBirthField(firstCustomer.BirthYear),
            BirthMonth = FormatBirthField(firstCustomer.BirthMonth),
            BirthDay = FormatBirthField(firstCustomer.BirthDay),
            BirthHour = firstCustomer.BirthHour,
            PrintDate = DateTime.Now.ToString("yyyy/MM/dd"),
            LunarStartDate = $"{rocYear}/01/15",
            LunarEndDate = $"{rocYear}/12/24",
            Amount = $"${firstOrder.Price:N0}元整",
            LampType = lamp.LampName
        };
    }

}
