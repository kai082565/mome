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
    public int? BirthYear { get; set; }       // 保留給「吉」(0) 的判斷，新資料不再填入年份數字
    public string? BirthYearText { get; set; } // 歲次，如「丙午」「甲子」
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public string? BirthHour { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 生肖：優先從歲次地支推算，否則從民國年換算
    private const string DiZhi = "子丑寅卯辰巳午未申酉戌亥";
    private static readonly string[] ZodiacAnimals = ["鼠", "牛", "虎", "兔", "龍", "蛇", "馬", "羊", "猴", "雞", "狗", "豬"];

    public string? Zodiac
    {
        get
        {
            if (BirthYearText?.Length >= 2)
            {
                var idx = DiZhi.IndexOf(BirthYearText[1]);
                if (idx >= 0) return ZodiacAnimals[idx];
            }
            if (BirthYear == null || BirthYear == 0) return null;
            var westernYear = BirthYear.Value + 1911;
            var index = ((westernYear - 4) % 12 + 12) % 12;
            return ZodiacAnimals[index];
        }
    }

    // 導航屬性
    public ICollection<LampOrder> LampOrders { get; set; } = new List<LampOrder>();
}
