namespace TempleLamp.Desktop.Models;

/// <summary>
/// 燈位資料
/// </summary>
public class LampSlot
{
    public int SlotId { get; set; }
    public int LampTypeId { get; set; }
    public string LampTypeName { get; set; } = string.Empty;
    public string SlotNumber { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Column { get; set; }
    public int Year { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LockedByWorkstation { get; set; }
    public DateTime? LockExpiresAt { get; set; }

    /// <summary>
    /// 顯示用狀態文字
    /// </summary>
    public string StatusDisplay => Status switch
    {
        "AVAILABLE" => "可選",
        "LOCKED" => "鎖定中",
        "SOLD" => "已售出",
        _ => Status
    };

    /// <summary>
    /// 是否可選取
    /// </summary>
    public bool IsAvailable => Status == "AVAILABLE";
}

/// <summary>
/// 燈種資料
/// </summary>
public class LampType
{
    public int LampTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public int AvailableSlotCount { get; set; }
}

/// <summary>
/// 鎖定燈位請求
/// </summary>
public class LockLampSlotRequest
{
    public int LockDurationSeconds { get; set; } = 300;
}

/// <summary>
/// 鎖定燈位回應
/// </summary>
public class LockLampSlotResponse
{
    public bool Success { get; set; }
    public LampSlot? Slot { get; set; }
    public DateTime? LockExpiresAt { get; set; }
}
