using Dapper;
using Microsoft.Data.SqlClient;
using TempleLamp.Api.DTOs.Responses;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 燈位資料存取介面
/// </summary>
public interface ILampSlotRepository
{
    Task<LampSlotResponse?> GetByIdAsync(int slotId);
    Task<IEnumerable<LampSlotResponse>> QueryAsync(int? lampTypeId, string? zone, bool availableOnly, int? year);
    Task<IEnumerable<LampTypeResponse>> GetLampTypesAsync();
    Task<LockResult> TryLockAsync(int slotId, string workstationId, int lockDurationSeconds, SqlTransaction? transaction = null);
    Task<bool> ReleaseAsync(int slotId, string workstationId);
    Task<bool> MarkAsSoldAsync(int slotId, SqlTransaction transaction);
    Task ReleaseExpiredLocksAsync();
}

/// <summary>
/// 燈位鎖定結果
/// </summary>
public class LockResult
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? LockExpiresAt { get; set; }
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

    /// <summary>
    /// 嘗試鎖定燈位（使用預存程序）
    /// </summary>
    public async Task<LockResult> TryLockAsync(int slotId, string workstationId, int lockDurationSeconds, SqlTransaction? transaction = null)
    {
        const string sql = "EXEC sp_TryLockLampSlot @SlotId, @WorkstationId, @LockDurationSeconds, @Success OUTPUT, @FailureReason OUTPUT, @LockExpiresAt OUTPUT";

        SqlConnection connection;
        bool ownsConnection = false;

        if (transaction != null)
        {
            connection = transaction.Connection!;
        }
        else
        {
            connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();
            ownsConnection = true;
        }

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("SlotId", slotId);
            parameters.Add("WorkstationId", workstationId);
            parameters.Add("LockDurationSeconds", lockDurationSeconds);
            parameters.Add("Success", dbType: System.Data.DbType.Boolean, direction: System.Data.ParameterDirection.Output);
            parameters.Add("FailureReason", dbType: System.Data.DbType.String, size: 200, direction: System.Data.ParameterDirection.Output);
            parameters.Add("LockExpiresAt", dbType: System.Data.DbType.DateTime, direction: System.Data.ParameterDirection.Output);

            await connection.ExecuteAsync(sql, parameters, transaction);

            var result = new LockResult
            {
                Success = parameters.Get<bool>("Success"),
                FailureReason = parameters.Get<string?>("FailureReason"),
                LockExpiresAt = parameters.Get<DateTime?>("LockExpiresAt")
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
        finally
        {
            if (ownsConnection)
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }
    }

    public async Task<bool> ReleaseAsync(int slotId, string workstationId)
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

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { SlotId = slotId, WorkstationId = workstationId });

        if (affected > 0)
        {
            _logger.LogInformation("燈位釋放成功: SlotId={SlotId}, Workstation={Workstation}", slotId, workstationId);
        }

        return affected > 0;
    }

    public async Task<bool> MarkAsSoldAsync(int slotId, SqlTransaction transaction)
    {
        const string sql = @"
            UPDATE LampSlots
            SET Status = 'SOLD',
                LockedByWorkstation = NULL,
                LockExpiresAt = NULL,
                UpdatedAt = GETDATE()
            WHERE SlotId = @SlotId";

        var affected = await transaction.Connection!.ExecuteAsync(sql, new { SlotId = slotId }, transaction);
        return affected > 0;
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
}
