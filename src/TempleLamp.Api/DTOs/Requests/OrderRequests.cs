using System.ComponentModel.DataAnnotations;

namespace TempleLamp.Api.DTOs.Requests;

/// <summary>
/// 建立訂單請求
/// </summary>
public class CreateOrderRequest
{
    /// <summary>
    /// 客戶 ID
    /// </summary>
    [Required(ErrorMessage = "客戶 ID 為必填")]
    public int CustomerId { get; set; }

    /// <summary>
    /// 燈位 ID 清單
    /// </summary>
    [Required(ErrorMessage = "至少需選擇一個燈位")]
    [MinLength(1, ErrorMessage = "至少需選擇一個燈位")]
    public List<int> LampSlotIds { get; set; } = new();

    /// <summary>
    /// 點燈者姓名（寫在燈位上的名字）
    /// </summary>
    [Required(ErrorMessage = "點燈者姓名為必填")]
    [StringLength(50, MinimumLength = 1)]
    public string LightingName { get; set; } = string.Empty;

    /// <summary>
    /// 祈福內容（選填）
    /// </summary>
    [StringLength(200)]
    public string? BlessingContent { get; set; }

    /// <summary>
    /// 備註（選填）
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// 確認訂單（付款）請求
/// </summary>
public class ConfirmOrderRequest
{
    /// <summary>
    /// 付款方式: CASH（現金）, CARD（信用卡）, TRANSFER（轉帳）
    /// </summary>
    [Required(ErrorMessage = "付款方式為必填")]
    [RegularExpression("^(CASH|CARD|TRANSFER)$", ErrorMessage = "付款方式必須為 CASH、CARD 或 TRANSFER")]
    public string PaymentMethod { get; set; } = "CASH";

    /// <summary>
    /// 實收金額
    /// </summary>
    [Required(ErrorMessage = "實收金額為必填")]
    [Range(0, 9999999, ErrorMessage = "金額必須大於等於 0")]
    public decimal AmountReceived { get; set; }

    /// <summary>
    /// 付款備註（選填）
    /// </summary>
    [StringLength(200)]
    public string? PaymentNotes { get; set; }
}

/// <summary>
/// 取消訂單請求
/// </summary>
public class CancelOrderRequest
{
    /// <summary>
    /// 取消原因
    /// </summary>
    [Required(ErrorMessage = "取消原因為必填")]
    [StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 列印收據請求
/// </summary>
public class PrintReceiptRequest
{
    /// <summary>
    /// 印表機名稱（選填，使用預設印表機）
    /// </summary>
    [StringLength(100)]
    public string? PrinterName { get; set; }

    /// <summary>
    /// 列印份數
    /// </summary>
    [Range(1, 5, ErrorMessage = "列印份數需介於 1-5")]
    public int Copies { get; set; } = 1;
}
