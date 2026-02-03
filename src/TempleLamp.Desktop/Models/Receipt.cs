namespace TempleLamp.Desktop.Models;

/// <summary>
/// 收據資料
/// </summary>
public class Receipt
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public Order Order { get; set; } = new();
    public string TempleName { get; set; } = string.Empty;
    public string TempleAddress { get; set; } = string.Empty;
    public string TemplePhone { get; set; } = string.Empty;
    public DateTime PrintTime { get; set; }
    public string FormattedContent { get; set; } = string.Empty;
}

/// <summary>
/// 列印請求
/// </summary>
public class PrintReceiptRequest
{
    public string? PrinterName { get; set; }
    public int Copies { get; set; } = 1;
}

/// <summary>
/// 列印結果
/// </summary>
public class PrintResult
{
    public bool Success { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public DateTime PrintTime { get; set; }
}
