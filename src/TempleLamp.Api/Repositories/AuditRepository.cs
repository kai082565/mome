using System.Data;
using Dapper;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 稽核紀錄資料存取介面
/// </summary>
public interface IAuditRepository
{
    // ===== 查詢（自建連線） =====
    Task LogAsync(AuditLogData data);
    Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null);

    // ===== 交易內操作（接收外部 Connection + Transaction） =====
    Task LogAsync(AuditLogData data, IDbConnection connection, IDbTransaction transaction);
    Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details, IDbConnection connection, IDbTransaction transaction);
}

/// <summary>
/// 稽核紀錄資料
/// </summary>
public class AuditLogData
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string WorkstationId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// 稽核紀錄資料存取實作
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AuditRepository> _logger;

    private const string InsertSql = @"
        INSERT INTO AuditLogs (Action, EntityType, EntityId, WorkstationId, OldValue, NewValue, Details, CreatedAt)
        VALUES (@Action, @EntityType, @EntityId, @WorkstationId, @OldValue, @NewValue, @Details, GETDATE())";

    public AuditRepository(IDbConnectionFactory connectionFactory, ILogger<AuditRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    #region 自建連線

    public async Task LogAsync(AuditLogData data)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(InsertSql, data);

        _logger.LogDebug("稽核紀錄: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, Workstation={Workstation}",
            data.Action, data.EntityType, data.EntityId, data.WorkstationId);
    }

    public async Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null)
    {
        var data = new AuditLogData
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            WorkstationId = workstationId,
            Details = details
        };

        await LogAsync(data);
    }

    #endregion

    #region 交易內操作（接收外部 Connection + Transaction）

    public async Task LogAsync(AuditLogData data, IDbConnection connection, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(InsertSql, data, transaction);

        _logger.LogDebug("稽核紀錄(交易內): Action={Action}, EntityType={EntityType}, EntityId={EntityId}, Workstation={Workstation}",
            data.Action, data.EntityType, data.EntityId, data.WorkstationId);
    }

    public async Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details, IDbConnection connection, IDbTransaction transaction)
    {
        var data = new AuditLogData
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            WorkstationId = workstationId,
            Details = details
        };

        await LogAsync(data, connection, transaction);
    }

    #endregion
}
