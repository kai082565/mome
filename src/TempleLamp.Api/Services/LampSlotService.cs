using System.Data;
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
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<LampSlotService> _logger;

    public LampSlotService(
        ILampSlotRepository lampSlotRepository,
        IAuditRepository auditRepository,
        IDbConnectionFactory connectionFactory,
        ILogger<LampSlotService> logger)
    {
        _lampSlotRepository = lampSlotRepository;
        _auditRepository = auditRepository;
        _connectionFactory = connectionFactory;
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

    /// <summary>
    /// 鎖定燈位（單一 Transaction 內完成）
    ///
    /// 流程：
    /// 1. 驗證燈位存在（Transaction 外）
    /// 2. 建立 Connection + Transaction
    /// 3. 呼叫 sp_TryLockLampSlot
    /// 4. 若鎖定失敗 → Rollback → 回傳錯誤
    /// 5. Insert AuditLogs
    /// 6. Commit Transaction
    /// </summary>
    public async Task<LockLampSlotResponse> LockAsync(int slotId, string workstationId, LockLampSlotRequest request)
    {
        // ===== Step 1: 驗證燈位存在（Transaction 外） =====
        var slot = await _lampSlotRepository.GetByIdAsync(slotId);
        if (slot == null)
        {
            throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
        }

        // ===== Step 2: 建立 Connection + Transaction =====
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // ===== Step 3: 呼叫 sp_TryLockLampSlot =====
            var lockResult = await _lampSlotRepository.TryLockAsync(
                slotId,
                workstationId,
                request.LockDurationSeconds,
                connection,
                transaction
            );

            // ===== Step 4: 若鎖定失敗 → Rollback → 回傳錯誤 =====
            if (!lockResult.Success)
            {
                _logger.LogWarning("燈位鎖定失敗: SlotId={SlotId}, Workstation={Workstation}, Reason={Reason}",
                    slotId, workstationId, lockResult.FailureReason);

                await RollbackTransactionAsync(transaction);

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

            // ===== Step 5: Insert AuditLogs =====
            await _auditRepository.LogAsync(
                "LOCK",
                "LampSlot",
                slotId,
                workstationId,
                $"鎖定至 {lockResult.LockExpiresAt:yyyy-MM-dd HH:mm:ss}",
                connection,
                transaction
            );

            // ===== Step 6: Commit Transaction =====
            transaction.Commit();

            _logger.LogInformation("燈位鎖定成功: SlotId={SlotId}, Workstation={Workstation}, ExpiresAt={ExpiresAt}",
                slotId, workstationId, lockResult.LockExpiresAt);

            // 取得更新後的燈位資訊
            var updatedSlot = await _lampSlotRepository.GetByIdAsync(slotId);

            return new LockLampSlotResponse
            {
                Success = true,
                Slot = updatedSlot,
                LockExpiresAt = lockResult.LockExpiresAt
            };
        }
        catch (BusinessException)
        {
            // 業務例外已經 Rollback，直接拋出
            throw;
        }
        catch (Exception ex)
        {
            // 系統例外：確保 Rollback 後包裝成 BusinessException
            await RollbackTransactionAsync(transaction);

            _logger.LogError(ex, "燈位鎖定失敗(系統錯誤): SlotId={SlotId}, Workstation={Workstation}",
                slotId, workstationId);

            throw new BusinessException(ErrorCodes.SLOT_LOCK_FAILED, "燈位鎖定失敗: " + ex.Message);
        }
    }

    /// <summary>
    /// 釋放燈位鎖定（單一 Transaction 內完成）
    ///
    /// 流程：
    /// 1. 驗證燈位存在與權限（Transaction 外）
    /// 2. 建立 Connection + Transaction
    /// 3. 執行 Release
    /// 4. Insert AuditLogs
    /// 5. Commit Transaction
    /// </summary>
    public async Task<bool> ReleaseAsync(int slotId, string workstationId)
    {
        // ===== Step 1: 驗證燈位存在與權限（Transaction 外） =====
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

        // ===== Step 2: 建立 Connection + Transaction =====
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // ===== Step 3: 執行 Release =====
            var released = await _lampSlotRepository.ReleaseAsync(slotId, workstationId, connection, transaction);

            if (released)
            {
                // ===== Step 4: Insert AuditLogs =====
                await _auditRepository.LogAsync(
                    "RELEASE",
                    "LampSlot",
                    slotId,
                    workstationId,
                    null,
                    connection,
                    transaction
                );
            }

            // ===== Step 5: Commit Transaction =====
            transaction.Commit();

            if (released)
            {
                _logger.LogInformation("燈位已釋放: SlotId={SlotId}, Workstation={Workstation}", slotId, workstationId);
            }

            return released;
        }
        catch (BusinessException)
        {
            await RollbackTransactionAsync(transaction);
            throw;
        }
        catch (Exception ex)
        {
            await RollbackTransactionAsync(transaction);

            _logger.LogError(ex, "燈位釋放失敗: SlotId={SlotId}, Workstation={Workstation}",
                slotId, workstationId);

            throw new BusinessException(ErrorCodes.SLOT_RELEASE_FAILED, "燈位釋放失敗: " + ex.Message);
        }
    }

    #region Private Methods

    /// <summary>
    /// 安全地 Rollback Transaction（忽略已完成或已 Rollback 的情況）
    /// </summary>
    private static async Task RollbackTransactionAsync(IDbTransaction transaction)
    {
        try
        {
            if (transaction.Connection != null)
            {
                transaction.Rollback();
            }
        }
        catch
        {
            // 忽略 Rollback 錯誤（可能已經 Rollback 或 Connection 已關閉）
        }

        await Task.CompletedTask;
    }

    #endregion
}
