namespace TempleLampSystem.Models;

public enum ConflictResolution
{
    UseLocal,      // 使用本地資料
    UseRemote,     // 使用雲端資料
    Merge,         // 合併（取較新者）
    Manual         // 手動解決
}

public class SyncConflict
{
    public int Id { get; set; }
    public SyncEntityType EntityType { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string LocalData { get; set; } = string.Empty;
    public string RemoteData { get; set; } = string.Empty;
    public DateTime LocalUpdatedAt { get; set; }
    public DateTime RemoteUpdatedAt { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public ConflictResolution? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
