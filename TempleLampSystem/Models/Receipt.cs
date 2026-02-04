namespace TempleLampSystem.Models;

/// <summary>
/// 點燈單據
/// </summary>
public class Receipt
{
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime PrintDate { get; set; } = DateTime.Now;

    // 宮廟資訊
    public string TempleName { get; set; } = string.Empty;
    public string TempleAddress { get; set; } = string.Empty;
    public string TemplePhone { get; set; } = string.Empty;

    // 客戶資訊
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerMobile { get; set; }
    public string? CustomerAddress { get; set; }

    // 點燈資訊
    public string LampName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }

    // 備註
    public string? Note { get; set; }

    /// <summary>
    /// 從 LampOrder 建立單據
    /// </summary>
    public static Receipt FromLampOrder(LampOrder order, Customer customer, Lamp lamp)
    {
        var settings = Services.AppSettings.Instance.Print;

        return new Receipt
        {
            ReceiptNo = $"R{DateTime.Now:yyyyMMddHHmmss}",
            PrintDate = DateTime.Now,
            TempleName = settings.TempleName,
            TempleAddress = settings.TempleAddress,
            TemplePhone = settings.TemplePhone,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            CustomerMobile = customer.Mobile,
            CustomerAddress = customer.Address,
            LampName = lamp.LampName,
            Year = order.Year,
            StartDate = order.StartDate,
            EndDate = order.EndDate,
            Price = order.Price
        };
    }
}
