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
                s.SlotId,
                s.LampTypeId,
                t.Name AS LampTypeName,
                s.SlotNumber,
                s.Zone,
                s.Row,
                s.[Column],
                s.Year,
                s.Price,
                s.Status,
                s.LockedByWorkstation,
                s.LockExpiresAt
            FROM LampSlots s
            INNER JOIN LampTypes t ON s.LampTypeId = t.LampTypeId
            WHERE s.SlotId = @SlotId";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<LampSlotResponse>(sql, new { SlotId = slotId });
    }

    public async Task<IEnumerable<LampSlotResponse>> QueryAsync(int? lampTypeId, string? zone, bool availableOnly, int? year)
    {
        var sql = @"
            SELECT
                s.SlotId,
                s.LampTypeId,
                t.Name AS LampTypeName,
                s.SlotNumber,
                s.Zone,
                s.Row,
                s.[Column],
                s.Year,
                s.Price,
                s.Status,
                s.LockedByWorkstation,
                s.LockExpiresAt
            FROM LampSlots s
            INNER JOIN LampTypes t ON s.LampTypeId = t.LampTypeId
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (lampTypeId.HasValue)
        {
            sql += " AND s.LampTypeId = @LampTypeId";
            parameters.Add("LampTypeId", lampTypeId.Value);
        }

        if (!string.IsNullOrEmpty(zone))
        {
            sql += " AND s.Zone = @Zone";
            parameters.Add("Zone", zone);
        }

        if (availableOnly)
        {
            sql += " AND s.Status = 'AVAILABLE'";
        }

        if (year.HasValue)
        {
            sql += " AND s.Year = @Year";
            parameters.Add("Year", year.Value);
        }
        else
        {
            sql += " AND s.Year = YEAR(GETDATE())";
        }

        sql += " ORDER BY s.Zone, s.Row, s.[Column]";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<LampSlotResponse>(sql, parameters);
    }

    public async Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync()
    {
        const string sql = @"
            SELECT
                t.LampTypeId,
                t.Name,
                t.Description,
                t.DefaultPrice,
                (SELECT COUNT(*) FROM LampSlots s
                 WHERE s.LampTypeId = t.LampTypeId
                 AND s.Status = 'AVAILABLE'
                 AND s.Year = YEAR(GETDATE())) AS AvailableSlotCount
            FROM LampTypes t
            WHERE t.IsActive = 1
            ORDER BY t.SortOrder, t.Name";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<LampTypeResponse>(sql);
    }

    public async Task ReleaseExpiredLocksAsync()
    {
        const string sql = @"
            UPDATE LampSlots
            SET Status = 'AVAILABLE',
                LockedByWorkstation = NULL,
                LockExpiresAt = NULL,
                UpdatedAt = GETDATE()
            WHERE Status = 'LOCKED'
              AND LockExpiresAt < GETDATE()";

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
    /// 嘗試鎖定燈位（使用預存程序 sp_TryLockLampSlot）
    /// 必須在 Transaction 內呼叫
    /// </summary>
    public async Task<LockResult> TryLockAsync(int slotId, string workstationId, int lockDurationSeconds, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = "EXEC sp_TryLockLampSlot @SlotId, @WorkstationId, @LockDurationSeconds, @Success OUTPUT, @FailureReason OUTPUT, @LockExpiresAt OUTPUT, @Price OUTPUT";

        var parameters = new DynamicParameters();
        parameters.Add("SlotId", slotId);
        parameters.Add("WorkstationId", workstationId);
        parameters.Add("LockDurationSeconds", lockDurationSeconds);
        parameters.Add("Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("FailureReason", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
        parameters.Add("LockExpiresAt", dbType: DbType.DateTime, direction: ParameterDirection.Output);
        parameters.Add("Price", dbType: DbType.Decimal, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters, transaction);

        var result = new LockResult
        {
            Success = parameters.Get<bool>("Success"),
            FailureReason = parameters.Get<string?>("FailureReason"),
            LockExpiresAt = parameters.Get<DateTime?>("LockExpiresAt"),
            Price = parameters.Get<decimal?>("Price") ?? 0
        };

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
    /// 釋放燈位鎖定（使用 UPDLOCK, ROWLOCK）
    /// </summary>
    public async Task<bool> ReleaseAsync(int slotId, string workstationId, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
            UPDATE LampSlots WITH (UPDLOCK, ROWLOCK)
            SET Status = 'AVAILABLE',
                LockedByWorkstation = NULL,
                LockExpiresAt = NULL,
                UpdatedAt = GETDATE()
            WHERE SlotId = @SlotId
              AND LockedByWorkstation = @WorkstationId
              AND Status = 'LOCKED'";

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
            UPDATE LampSlots
            SET Status = 'SOLD',
                LockedByWorkstation = NULL,
                LockExpiresAt = NULL,
                UpdatedAt = GETDATE()
            WHERE SlotId = @SlotId";

        var affected = await connection.ExecuteAsync(sql, new { SlotId = slotId }, transaction);
        return affected > 0;
    }

    #endregion
}
