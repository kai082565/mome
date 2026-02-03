using System.Data;
using Dapper;
using TempleLamp.Api.DTOs.Responses;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 燈位資料存取介面
/// </summary>
public interface ILampSlotRepository
{
    // ===== 查詢（自建連線） =====
    Task<LampSlotResponse?> GetByIdAsync(int slotId);
    Task<IEnumerable<LampSlotResponse>> QueryAsync(int? lampTypeId, string? zone, bool availableOnly, int? year);
    Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync();
    Task ReleaseExpiredLocksAsync();

    // ===== 交易內操作（接收外部 Connection + Transaction） =====
    Task<LockResult> TryLockAsync(int slotId, string workstationId, int lockDurationSeconds, IDbConnection connection, IDbTransaction transaction);
    Task<bool> ReleaseAsync(int slotId, string workstationId, IDbConnection connection, IDbTransaction transaction);
    Task<bool> MarkAsSoldAsync(int slotId, IDbConnection connection, IDbTransaction transaction);
}

/// <summary>
/// 燈位鎖定結果
/// </summary>
public class LockResult
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? LockExpiresAt { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// 燈位資料存取實作
/// </summary>
public class LampSlotRepository : ILampSlotRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<LampSlotRepository> _logger;

    public LampSlotRepository(IDbConnectionFactory connectionFactory, ILogger<LampSlotRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    #region 查詢（自建連線）

    public async Task<LampSlotResponse?> GetByIdAsync(int slotId)
    {
        const string sql = @"
            SELECT
                s.""slot_id"" AS ""SlotId"",
                s.""lamp_type_id"" AS ""LampTypeId"",
                t.""name"" AS ""LampTypeName"",
                s.""slot_number"" AS ""SlotNumber"",
                s.""zone"" AS ""Zone"",
                s.""row"" AS ""Row"",
                s.""column"" AS ""Column"",
                s.""year"" AS ""Year"",
                s.""price"" AS ""Price"",
                s.""status"" AS ""Status"",
                s.""locked_by_workstation"" AS ""LockedByWorkstation"",
                s.""lock_expires_at"" AS ""LockExpiresAt""
            FROM public.lamp_slots s
            INNER JOIN public.lamp_types t ON s.""lamp_type_id"" = t.""lamp_type_id""
            WHERE s.""slot_id"" = @SlotId";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<LampSlotResponse>(sql, new { SlotId = slotId });
    }

    public async Task<IEnumerable<LampSlotResponse>> QueryAsync(int? lampTypeId, string? zone, bool availableOnly, int? year)
    {
        var sql = @"
            SELECT
                s.""slot_id"" AS ""SlotId"",
                s.""lamp_type_id"" AS ""LampTypeId"",
                t.""name"" AS ""LampTypeName"",
                s.""slot_number"" AS ""SlotNumber"",
                s.""zone"" AS ""Zone"",
                s.""row"" AS ""Row"",
                s.""column"" AS ""Column"",
                s.""year"" AS ""Year"",
                s.""price"" AS ""Price"",
                s.""status"" AS ""Status"",
                s.""locked_by_workstation"" AS ""LockedByWorkstation"",
                s.""lock_expires_at"" AS ""LockExpiresAt""
            FROM public.lamp_slots s
            INNER JOIN public.lamp_types t ON s.""lamp_type_id"" = t.""lamp_type_id""
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (lampTypeId.HasValue)
        {
            sql += @" AND s.""lamp_type_id"" = @LampTypeId";
            parameters.Add("LampTypeId", lampTypeId.Value);
        }

        if (!string.IsNullOrEmpty(zone))
        {
            sql += @" AND s.""zone"" = @Zone";
            parameters.Add("Zone", zone);
        }

        if (availableOnly)
        {
            sql += @" AND s.""status"" = 'AVAILABLE'";
        }

        if (year.HasValue)
        {
            sql += @" AND s.""year"" = @Year";
            parameters.Add("Year", year.Value);
        }
        else
        {
            sql += @" AND s.""year"" = EXTRACT(YEAR FROM NOW())";
        }

        sql += @" ORDER BY s.""zone"", s.""row"", s.""column""";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<LampSlotResponse>(sql, parameters);
    }

    public async Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync()
    {
        const string sql = @"
            SELECT
                t.""lamp_type_id"" AS ""LampTypeId"",
                t.""name"" AS ""Name"",
                t.""description"" AS ""Description"",
                t.""default_price"" AS ""DefaultPrice"",
                (SELECT COUNT(*) FROM public.lamp_slots s
                 WHERE s.""lamp_type_id"" = t.""lamp_type_id""
                 AND s.""status"" = 'AVAILABLE'
                 AND s.""year"" = EXTRACT(YEAR FROM NOW())) AS ""AvailableSlotCount""
            FROM public.lamp_types t
            WHERE t.""is_active"" = true
            ORDER BY t.""sort_order"", t.""name""";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<LampTypeResponse>(sql);
    }

    public async Task ReleaseExpiredLocksAsync()
    {
        const string sql = @"
            UPDATE public.lamp_slots
            SET ""status"" = 'AVAILABLE',
                ""locked_by_workstation"" = NULL,
                ""lock_expires_at"" = NULL,
                ""updated_at"" = NOW()
            WHERE ""status"" = 'LOCKED'
              AND ""lock_expires_at"" < NOW()";

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql);

        if (affected > 0)
        {
            _logger.LogInformation("釋放過期鎖定: 共 {Count} 個燈位", affected);
        }
    }

    #endregion

    #region 交易內操作（接收外部 Connection + Transaction）

    /// <summary>
    /// 嘗試鎖定燈位（使用 PostgreSQL 函數 try_lock_lamp_slot）
    /// 必須在 Transaction 內呼叫
    /// </summary>
    public async Task<LockResult> TryLockAsync(int slotId, string workstationId, int lockDurationSeconds, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
            SELECT
                ""success"" AS ""Success"",
                ""failure_reason"" AS ""FailureReason"",
                ""lock_expires_at"" AS ""LockExpiresAt"",
                ""price"" AS ""Price""
            FROM public.try_lock_lamp_slot(@SlotId, @WorkstationId, @LockDurationSeconds)";

        var result = await connection.QuerySingleOrDefaultAsync<LockResult>(
            sql,
            new { SlotId = slotId, WorkstationId = workstationId, LockDurationSeconds = lockDurationSeconds },
            transaction
        );

        result ??= new LockResult { Success = false, FailureReason = "函數呼叫失敗" };

        if (result.Success)
        {
            _logger.LogInformation("燈位鎖定成功: SlotId={SlotId}, Workstation={Workstation}, ExpiresAt={ExpiresAt}",
                slotId, workstationId, result.LockExpiresAt);
        }
        else
        {
            _logger.LogWarning("燈位鎖定失敗: SlotId={SlotId}, Reason={Reason}", slotId, result.FailureReason);
        }

        return result;
    }

    /// <summary>
    /// 釋放燈位鎖定（PostgreSQL 使用 FOR UPDATE）
    /// </summary>
    public async Task<bool> ReleaseAsync(int slotId, string workstationId, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
            UPDATE public.lamp_slots
            SET ""status"" = 'AVAILABLE',
                ""locked_by_workstation"" = NULL,
                ""lock_expires_at"" = NULL,
                ""updated_at"" = NOW()
            WHERE ""slot_id"" = @SlotId
              AND ""locked_by_workstation"" = @WorkstationId
              AND ""status"" = 'LOCKED'";

        var affected = await connection.ExecuteAsync(sql, new { SlotId = slotId, WorkstationId = workstationId }, transaction);

        if (affected > 0)
        {
            _logger.LogInformation("燈位釋放成功: SlotId={SlotId}, Workstation={Workstation}", slotId, workstationId);
        }

        return affected > 0;
    }

    /// <summary>
    /// 標記燈位為已售出
    /// </summary>
    public async Task<bool> MarkAsSoldAsync(int slotId, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
            UPDATE public.lamp_slots
            SET ""status"" = 'SOLD',
                ""locked_by_workstation"" = NULL,
                ""lock_expires_at"" = NULL,
                ""updated_at"" = NOW()
            WHERE ""slot_id"" = @SlotId";

        var affected = await connection.ExecuteAsync(sql, new { SlotId = slotId }, transaction);
        return affected > 0;
    }

    #endregion
}
