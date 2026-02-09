namespace TempleLampSystem.Models;

/// <summary>
/// 燈的種類（如：太歲燈、光明燈、平安燈）
/// </summary>
public class Lamp
{
    public int Id { get; set; }
    public string LampCode { get; set; } = string.Empty;   // 代碼，如 "TAISUI", "GUANGMING"
    public string LampName { get; set; } = string.Empty;   // 名稱，如 "太歲燈", "光明燈"
    public string? Temple { get; set; }                    // 宮廟別，如 "鳳屏宮", "天后宮"
    public string? Deity { get; set; }                     // 神明別，如 "太歲星君", "媽祖"
    public int MaxQuota { get; set; }                      // 年度限量（0 = 不限量）

    // 導航屬性
    public ICollection<LampOrder> LampOrders { get; set; } = new List<LampOrder>();

    // 顯示用（燈種 + 宮廟 + 神明）
    public string DisplayName
    {
        get
        {
            var parts = new List<string> { LampName };
            if (!string.IsNullOrEmpty(Temple)) parts.Add(Temple);
            if (!string.IsNullOrEmpty(Deity)) parts.Add(Deity);
            return string.Join(" - ", parts);
        }
    }
}
