namespace TempleLamp.Api.DTOs.Responses;

/// <summary>
/// 客戶資訊回應
/// </summary>
public class CustomerResponse
{
    /// <summary>
    /// 客戶 ID
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 客戶姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 電話號碼
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 累計點燈次數
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 客戶搜尋結果
/// </summary>
public class CustomerSearchResponse
{
    /// <summary>
    /// 搜尋結果清單
    /// </summary>
    public List<CustomerResponse> Customers { get; set; } = new();

    /// <summary>
    /// 結果總數
    /// </summary>
    public int TotalCount { get; set; }
}
