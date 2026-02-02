using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Exceptions;
using TempleLamp.Api.Repositories;

namespace TempleLamp.Api.Services;

/// <summary>
/// 燈位服務介面
/// </summary>
public interface ILampSlotService
{
    Task<LampSlotResponse> GetByIdAsync(int slotId);
    Task<IEnumerable<LampSlotResponse>> QueryAsync(LampSlotQueryRequest request);
    Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync();
    Task<LockLampSlotResponse> LockAsync(int slotId, string workstationId, LockLampSlotRequest request);
    Task<bool> ReleaseAsync(int slotId, string workstationId);
}

/// <summary>
/// 燈位服務實作
/// </summary>
public class LampSlotService : ILampSlotService
{
    private readonly ILampSlotRepository _lampSlotRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<LampSlotService> _logger;

    public LampSlotService(
        ILampSlotRepository lampSlotRepository,
        IAuditRepository auditRepository,
        ILogger<LampSlotService> logger)
    {
        _lampSlotRepository = lampSlotRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<LampSlotResponse> GetByIdAsync(int slotId)
    {
        var slot = await _lampSlotRepository.GetByIdAsync(slotId);

        if (slot == null)
        {
            throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
        }

        // 檢查是否過期鎖定，若是則清除
        if (slot.Status == "LOCKED" && slot.LockExpiresAt.HasValue && slot.LockExpiresAt.Value < DateTime.Now)
        {
            await _lampSlotRepository.ReleaseExpiredLocksAsync();
            slot = await _lampSlotRepository.GetByIdAsync(slotId);
        }

        return slot!;
    }

    public async Task<IEnumerable<LampSlotResponse>> QueryAsync(LampSlotQueryRequest request)
    {
        // 先清理過期鎖定
        await _lampSlotRepository.ReleaseExpiredLocksAsync();

        return await _lampSlotRepository.QueryAsync(
            request.LampTypeId,
            request.Zone,
            request.AvailableOnly,
            request.Year
        );
    }

    public async Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync()
    {
        return await _lampSlotRepository.GetLampTypesAsync();
    }

    public async Task<LockLampSlotResponse> LockAsync(int slotId, string workstationId, LockLampSlotRequest request)
    {
        // 確認燈位存在
        var slot = await _lampSlotRepository.GetByIdAsync(slotId);
        if (slot == null)
        {
            throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
        }

        // 嘗試鎖定
        var lockResult = await _lampSlotRepository.TryLockAsync(slotId, workstationId, request.LockDurationSeconds);

        if (!lockResult.Success)
        {
            _logger.LogWarning("燈位鎖定失敗: SlotId={SlotId}, Workstation={Workstation}, Reason={Reason}",
                slotId, workstationId, lockResult.FailureReason);

            // 根據失敗原因決定例外類型
            if (lockResult.FailureReason?.Contains("已被鎖定") == true ||
                lockResult.FailureReason?.Contains("LOCKED") == true)
            {
                throw new ConflictException(ErrorCodes.SLOT_LOCKED, lockResult.FailureReason);
            }

            if (lockResult.FailureReason?.Contains("已售出") == true ||
                lockResult.FailureReason?.Contains("SOLD") == true)
            {
                throw new ConflictException(ErrorCodes.SLOT_NOT_AVAILABLE, "燈位已售出");
            }

            throw new BusinessException(ErrorCodes.SLOT_LOCK_FAILED, lockResult.FailureReason ?? "鎖定失敗");
        }

        // 記錄稽核
        await _auditRepository.LogAsync("LOCK", "LampSlot", slotId, workstationId,
            $"鎖定至 {lockResult.LockExpiresAt:yyyy-MM-dd HH:mm:ss}");

        // 取得更新後的燈位資訊
        var updatedSlot = await _lampSlotRepository.GetByIdAsync(slotId);

        return new LockLampSlotResponse
        {
            Success = true,
            Slot = updatedSlot,
            LockExpiresAt = lockResult.LockExpiresAt
        };
    }

    public async Task<bool> ReleaseAsync(int slotId, string workstationId)
    {
        // 確認燈位存在
        var slot = await _lampSlotRepository.GetByIdAsync(slotId);
        if (slot == null)
        {
            throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
        }

        // 確認是否為同一工作站鎖定
        if (slot.Status == "LOCKED" && slot.LockedByWorkstation != workstationId)
        {
            throw new BusinessException(ErrorCodes.SLOT_NOT_LOCKED_BY_YOU, "此燈位非由您的工作站鎖定");
        }

        var released = await _lampSlotRepository.ReleaseAsync(slotId, workstationId);

        if (released)
        {
            await _auditRepository.LogAsync("RELEASE", "LampSlot", slotId, workstationId);
            _logger.LogInformation("燈位已釋放: SlotId={SlotId}, Workstation={Workstation}", slotId, workstationId);
        }

        return released;
    }
}
