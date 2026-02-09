using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface ILampOrderService
{
    /// <summary>
    /// 檢查該客戶是否可以點此燈種
    /// </summary>
    Task<bool> CanOrderLampAsync(Guid customerId, int lampId);

    /// <summary>
    /// 建立點燈紀錄
    /// </summary>
    Task<LampOrder> CreateLampOrderAsync(Guid customerId, int lampId, decimal price, string? note = null);

    /// <summary>
    /// 取得即將到期的點燈紀錄
    /// </summary>
    Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry = 30);

    /// <summary>
    /// 檢查並回傳不可點燈的原因
    /// </summary>
    Task<string?> GetCannotOrderReasonAsync(Guid customerId, int lampId);

    /// <summary>
    /// 取得燈種今年的剩餘名額（-1 表示不限量）
    /// </summary>
    Task<int> GetRemainingQuotaAsync(int lampId);
}
