namespace TempleLampSystem.Models;

/// <summary>
/// 燈的種類（如：太歲燈、光明燈、平安燈）
/// </summary>
public class Lamp
{
    public int Id { get; set; }
    public string LampCode { get; set; } = string.Empty;   // 代碼，如 "TAISUI", "GUANGMING"
    public string LampName { get; set; } = string.Empty;   // 名稱，如 "太歲燈", "光明燈"

    // 導航屬性
    public ICollection<LampOrder> LampOrders { get; set; } = new List<LampOrder>();
}
