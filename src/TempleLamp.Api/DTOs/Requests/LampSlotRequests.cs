using System.ComponentModel.DataAnnotations;

namespace TempleLamp.Api.DTOs.Requests;

/// <summary>
/// 燈位鎖定請求
/// </summary>
public class LockLampSlotRequest
{
    /// <summary>
    /// 鎖定期間（秒），預設 300 秒（5分鐘）
    /// </summary>
    [Range(60, 600, ErrorMessage = "鎖定期間需介於 60-600 秒")]
    public int LockDurationSeconds { get; set; } = 300;
}

/// <summary>
/// 燈位查詢請求
/// </summary>
public class LampSlotQueryRequest
{
    /// <summary>
    /// 燈種 ID（選填）
    /// </summary>
    public int? LampTypeId { get; set; }

    /// <summary>
    /// 區域（選填）
    /// </summary>
    [StringLength(50)]
    public string? Zone { get; set; }

    /// <summary>
    /// 僅顯示可用燈位
    /// </summary>
    public bool AvailableOnly { get; set; } = true;

    /// <summary>
    /// 年度（選填，預設當年）
    /// </summary>
    public int? Year { get; set; }
}
