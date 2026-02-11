namespace TempleLampSystem.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public string? Village { get; set; }
    public string? PostalCode { get; set; }
    public string? CustomerCode { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public string? BirthHour { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 生肖計算屬性（不存 DB）
    private static readonly string[] ZodiacAnimals = ["鼠", "牛", "虎", "兔", "龍", "蛇", "馬", "羊", "猴", "雞", "狗", "豬"];

    public string? Zodiac
    {
        get
        {
            if (BirthYear == null) return null;
            var westernYear = BirthYear.Value + 1911;
            var index = ((westernYear - 4) % 12 + 12) % 12;
            return ZodiacAnimals[index];
        }
    }

    // 導航屬性
    public ICollection<LampOrder> LampOrders { get; set; } = new List<LampOrder>();
}
