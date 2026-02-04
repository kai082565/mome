namespace TempleLampSystem.Services;

public interface IAutoSyncService
{
    void Start();
    void Stop();
    bool IsRunning { get; }
    event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
}

public class SyncStatusEventArgs : EventArgs
{
    public bool IsOnline { get; set; }
    public int PendingCount { get; set; }
    public string? Message { get; set; }
}
