namespace TempleLamp.Desktop.Models;

/// <summary>
/// 客戶資料
/// </summary>
public class Customer
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalOrders { get; set; }
}

/// <summary>
/// 建立客戶請求
/// </summary>
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
}
