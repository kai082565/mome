namespace TempleLampSystem.Models;

public enum SyncOperation
{
    Insert,
    Update,
    Delete
}

public enum SyncEntityType
{
    Customer,
    LampOrder
}

public class SyncQueueItem
{
    public int Id { get; set; }
    public SyncEntityType EntityType { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public SyncOperation Operation { get; set; }
    public string? JsonData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
}
