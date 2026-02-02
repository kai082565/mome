using Dapper;
using Microsoft.Data.SqlClient;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 稽核紀錄資料存取介面
/// </summary>
public interface IAuditRepository
{
    Task LogAsync(AuditLogData data, SqlTransaction? transaction = null);
    Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null, SqlTransaction? transaction = null);
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

    public AuditRepository(IDbConnectionFactory connectionFactory, ILogger<AuditRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogData data, SqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO AuditLogs (Action, EntityType, EntityId, WorkstationId, OldValue, NewValue, Details, CreatedAt)
            VALUES (@Action, @EntityType, @EntityId, @WorkstationId, @OldValue, @NewValue, @Details, GETDATE())";

        if (transaction != null)
        {
            await transaction.Connection!.ExecuteAsync(sql, data, transaction);
        }
        else
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(sql, data);
        }

        _logger.LogDebug("稽核紀錄: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, Workstation={Workstation}",
            data.Action, data.EntityType, data.EntityId, data.WorkstationId);
    }

    public async Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null, SqlTransaction? transaction = null)
    {
        var data = new AuditLogData
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            WorkstationId = workstationId,
            Details = details
        };

        await LogAsync(data, transaction);
    }
}
