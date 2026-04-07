namespace TempleLampSystem.Services;

public interface IBackupService
{
    void Start();
    void Stop();
    /// <summary>
    /// 立即執行一次備份，回傳備份檔案路徑；失敗時回傳 null
    /// </summary>
    Task<string?> BackupNowAsync();
}
