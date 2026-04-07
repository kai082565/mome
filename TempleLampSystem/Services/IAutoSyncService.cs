namespace TempleLampSystem.Services;

public interface IAutoSyncService
{
    void Start();
    void Stop();
    void Pause();
    void Resume();
    bool IsRunning { get; }
    event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
}

public class SyncStatusEventArgs : EventArgs
{
    public bool IsOnline { get; set; }
    public int PendingCount { get; set; }
    public string? Message { get; set; }
    /// <summary>本次同步有從雲端下載新資料（其他電腦的變更）</summary>
    public bool HasNewData { get; set; }
}
