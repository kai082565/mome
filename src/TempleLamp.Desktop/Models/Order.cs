namespace TempleLamp.Desktop.Models;

/// <summary>
/// 訂單資料
/// </summary>
public class Order
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string LightingName { get; set; } = string.Empty;
    public string? BlessingContent { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByWorkstation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public Payment? Payment { get; set; }

    /// <summary>
    /// 狀態顯示文字
    /// </summary>
    public string StatusDisplay => Status switch
    {
        "PENDING" => "待付款",
        "CONFIRMED" => "已確認",
        "CANCELLED" => "已取消",
        _ => Status
    };
}

/// <summary>
/// 訂單明細
/// </summary>
public class OrderItem
{
    public int OrderItemId { get; set; }
    public int SlotId { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public string LampTypeName { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Year { get; set; }
}

/// <summary>
/// 付款資料
/// </summary>
public class Payment
{
    public int PaymentId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal ChangeAmount { get; set; }
    public DateTime PaymentTime { get; set; }
    public string ReceivedByWorkstation { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// 建立訂單請求
/// </summary>
public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<int> LampSlotIds { get; set; } = new();
    public string LightingName { get; set; } = string.Empty;
    public string? BlessingContent { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// 確認訂單請求
/// </summary>
public class ConfirmOrderRequest
{
    public string PaymentMethod { get; set; } = "CASH";
    public decimal AmountReceived { get; set; }
    public string? PaymentNotes { get; set; }
}
