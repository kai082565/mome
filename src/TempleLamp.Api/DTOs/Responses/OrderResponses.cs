namespace TempleLamp.Api.DTOs.Responses;

/// <summary>
/// 訂單資訊回應
/// </summary>
public class OrderResponse
{
    /// <summary>
    /// 訂單 ID
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// 訂單編號（如 ORD-20240101-0001）
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// 客戶 ID
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 客戶姓名
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// 客戶電話
    /// </summary>
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// 點燈者姓名
    /// </summary>
    public string LightingName { get; set; } = string.Empty;

    /// <summary>
    /// 祈福內容
    /// </summary>
    public string? BlessingContent { get; set; }

    /// <summary>
    /// 訂單狀態: PENDING（待付款）, CONFIRMED（已確認）, CANCELLED（已取消）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 訂單總金額
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 訂單明細（燈位）
    /// </summary>
    public List<OrderItemResponse> Items { get; set; } = new();

    /// <summary>
    /// 付款資訊（若已付款）
    /// </summary>
    public PaymentResponse? Payment { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 建立者工作站
    /// </summary>
    public string CreatedByWorkstation { get; set; } = string.Empty;

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 訂單明細回應
/// </summary>
public class OrderItemResponse
{
    /// <summary>
    /// 明細 ID
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 燈位 ID
    /// </summary>
    public int SlotId { get; set; }

    /// <summary>
    /// 燈位編號
    /// </summary>
    public string SlotNumber { get; set; } = string.Empty;

    /// <summary>
    /// 燈種名稱
    /// </summary>
    public string LampTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 區域
    /// </summary>
    public string Zone { get; set; } = string.Empty;

    /// <summary>
    /// 單價
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 年度
    /// </summary>
    public int Year { get; set; }
}

/// <summary>
/// 付款資訊回應
/// </summary>
public class PaymentResponse
{
    /// <summary>
    /// 付款 ID
    /// </summary>
    public int PaymentId { get; set; }

    /// <summary>
    /// 付款方式
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// 應付金額
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// 實收金額
    /// </summary>
    public decimal AmountReceived { get; set; }

    /// <summary>
    /// 找零金額
    /// </summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>
    /// 付款時間
    /// </summary>
    public DateTime PaymentTime { get; set; }

    /// <summary>
    /// 收款工作站
    /// </summary>
    public string ReceivedByWorkstation { get; set; } = string.Empty;

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 收據資訊回應
/// </summary>
public class ReceiptResponse
{
    /// <summary>
    /// 收據編號
    /// </summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>
    /// 訂單資訊
    /// </summary>
    public OrderResponse Order { get; set; } = new();

    /// <summary>
    /// 宮廟名稱
    /// </summary>
    public string TempleName { get; set; } = string.Empty;

    /// <summary>
    /// 宮廟地址
    /// </summary>
    public string TempleAddress { get; set; } = string.Empty;

    /// <summary>
    /// 宮廟電話
    /// </summary>
    public string TemplePhone { get; set; } = string.Empty;

    /// <summary>
    /// 列印時間
    /// </summary>
    public DateTime PrintTime { get; set; }

    /// <summary>
    /// 收據內容（可直接列印的格式化文字）
    /// </summary>
    public string FormattedContent { get; set; } = string.Empty;
}

/// <summary>
/// 列印結果回應
/// </summary>
public class PrintResultResponse
{
    /// <summary>
    /// 是否列印成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 列印的收據編號
    /// </summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>
    /// 使用的印表機
    /// </summary>
    public string PrinterName { get; set; } = string.Empty;

    /// <summary>
    /// 列印時間
    /// </summary>
    public DateTime PrintTime { get; set; }

    /// <summary>
    /// 錯誤訊息（若失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
