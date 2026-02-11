namespace TempleLampSystem.Models;

/// <summary>
/// 客戶資料信件（用於寄信提醒點燈到期）
/// </summary>
public class CustomerInfoLetter
{
    public DateTime PrintDate { get; set; } = DateTime.Now;

    // 宮廟資訊
    public string TempleName { get; set; } = string.Empty;
    public string TempleAddress { get; set; } = string.Empty;
    public string TemplePhone { get; set; } = string.Empty;

    // 客戶資訊
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerMobile { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerPostalCode { get; set; }
    public string? CustomerVillage { get; set; }

    // 點燈紀錄
    public List<LampOrderInfo> Orders { get; set; } = new();

    /// <summary>
    /// 從 Customer（含 LampOrders）建立信件資料
    /// </summary>
    public static CustomerInfoLetter FromCustomer(Customer customer)
    {
        var settings = Services.AppSettings.Instance.Print;

        return new CustomerInfoLetter
        {
            PrintDate = DateTime.Now,
            TempleName = settings.TempleName,
            TempleAddress = settings.TempleAddress,
            TemplePhone = settings.TemplePhone,
            CustomerName = customer.Name,
            CustomerCode = customer.CustomerCode,
            CustomerPhone = customer.Phone,
            CustomerMobile = customer.Mobile,
            CustomerAddress = customer.Address,
            CustomerPostalCode = customer.PostalCode,
            CustomerVillage = customer.Village,
            Orders = customer.LampOrders
                .OrderByDescending(o => o.Year)
                .ThenBy(o => o.Lamp.LampName)
                .Select(o => new LampOrderInfo
                {
                    LampName = o.Lamp.LampName,
                    Year = o.Year,
                    StartDate = o.StartDate,
                    EndDate = o.EndDate,
                    Price = o.Price,
                    IsExpired = o.EndDate <= DateTime.Now.Date,
                    IsActive = o.StartDate <= DateTime.Now.Date && o.EndDate > DateTime.Now.Date
                })
                .ToList()
        };
    }
}

public class LampOrderInfo
{
    public string LampName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public bool IsExpired { get; set; }
    public bool IsActive { get; set; }

    public string StatusText
    {
        get
        {
            if (IsExpired) return "已過期";
            if (IsActive) return "有效";
            return "未開始";
        }
    }
}
