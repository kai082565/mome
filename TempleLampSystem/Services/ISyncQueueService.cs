using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface ISyncQueueService
{
    Task EnqueueAsync<T>(T entity, SyncOperation operation) where T : class;
    Task<SyncQueueResult> ProcessQueueAsync();
    Task<int> GetPendingCountAsync();
    Task<bool> IsOnlineAsync();
}

public class SyncQueueResult
{
    public int TotalItems { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
