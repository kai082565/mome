using TempleLamp.Api.Repositories;

namespace TempleLamp.Api.Services;

/// <summary>
/// 稽核服務介面
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null);
}

/// <summary>
/// 稽核服務實作
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IAuditRepository auditRepository, ILogger<AuditService> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, int entityId, string workstationId, string? details = null)
    {
        try
        {
            await _auditRepository.LogAsync(action, entityType, entityId, workstationId, details);
        }
        catch (Exception ex)
        {
            // 稽核失敗不應影響業務流程，僅記錄警告
            _logger.LogWarning(ex, "稽核記錄失敗: Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                action, entityType, entityId);
        }
    }
}
