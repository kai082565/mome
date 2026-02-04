namespace TempleLampSystem.Models;

/// <summary>
/// 客戶搜尋結果顯示用
/// </summary>
public class CustomerDisplayModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public List<LampOrderDisplayModel> Orders { get; set; } = new();

    public string DisplayPhone => string.IsNullOrEmpty(Phone) ? Mobile ?? "" : Phone;
    public int ActiveOrderCount => Orders.Count(o => o.IsActive);
}

/// <summary>
/// 點燈紀錄顯示用
/// </summary>
public class LampOrderDisplayModel
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string LampName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }

    public bool IsExpired => EndDate < DateTime.UtcNow.Date;
    public bool IsActive => StartDate <= DateTime.UtcNow.Date && EndDate >= DateTime.UtcNow.Date;
    public bool IsExpiringSoon => IsActive && (EndDate - DateTime.UtcNow.Date).Days <= 30;
    public int DaysLeft => Math.Max(0, (EndDate - DateTime.UtcNow.Date).Days);

    public string StatusText
    {
        get
        {
            if (IsExpired) return "已過期";
            if (IsExpiringSoon) return $"即將到期（{DaysLeft}天）";
            return "有效";
        }
    }
}
