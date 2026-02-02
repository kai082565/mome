namespace TempleLamp.Api.DTOs.Responses;

/// <summary>
/// 燈位資訊回應
/// </summary>
public class LampSlotResponse
{
    /// <summary>
    /// 燈位 ID
    /// </summary>
    public int SlotId { get; set; }

    /// <summary>
    /// 燈種 ID
    /// </summary>
    public int LampTypeId { get; set; }

    /// <summary>
    /// 燈種名稱
    /// </summary>
    public string LampTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 燈位編號（如 A-001）
    /// </summary>
    public string SlotNumber { get; set; } = string.Empty;

    /// <summary>
    /// 區域
    /// </summary>
    public string Zone { get; set; } = string.Empty;

    /// <summary>
    /// 排
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// 列
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// 年度
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// 價格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 狀態: AVAILABLE（可用）, LOCKED（鎖定中）, SOLD（已售出）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 鎖定者工作站（若已鎖定）
    /// </summary>
    public string? LockedByWorkstation { get; set; }

    /// <summary>
    /// 鎖定到期時間（若已鎖定）
    /// </summary>
    public DateTime? LockExpiresAt { get; set; }
}

/// <summary>
/// 燈位鎖定結果回應
/// </summary>
public class LockLampSlotResponse
{
    /// <summary>
    /// 是否鎖定成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 燈位資訊
    /// </summary>
    public LampSlotResponse? Slot { get; set; }

    /// <summary>
    /// 鎖定到期時間
    /// </summary>
    public DateTime? LockExpiresAt { get; set; }

    /// <summary>
    /// 失敗原因（若鎖定失敗）
    /// </summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// 燈種資訊回應
/// </summary>
public class LampTypeResponse
{
    /// <summary>
    /// 燈種 ID
    /// </summary>
    public int LampTypeId { get; set; }

    /// <summary>
    /// 燈種名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 說明
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 預設價格
    /// </summary>
    public decimal DefaultPrice { get; set; }

    /// <summary>
    /// 可用燈位數
    /// </summary>
    public int AvailableSlotCount { get; set; }
}
