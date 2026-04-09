using System.IO;

namespace TempleLampSystem.Services;

public class BackupService : IBackupService
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempleLampSystem", "TempleLamp.db");

    private static readonly string BackupFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempleLampSystem", "Backups");

    // 每天晚上幾點觸發備份
    private const int BackupHour = 19;

    private CancellationTokenSource? _cts;
    private DateTime? _lastBackupDate;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunBackupLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public async Task<string?> BackupNowAsync()
    {
        try
        {
            Directory.CreateDirectory(BackupFolder);

            if (!File.Exists(DbPath))
                return null;

            var now = DateTime.Now;
            var fileName = $"TempleLamp_{now.Year}年{now.Month:D2}月{now.Day:D2}日_{now.Hour:D2}時{now.Minute:D2}分.db";
            var destPath = Path.Combine(BackupFolder, fileName);

            // 使用 SQLite backup API 安全方式：直接複製（WAL 模式下 File.Copy 是安全的）
            var originalSize = new FileInfo(DbPath).Length;
            await Task.Run(() => File.Copy(DbPath, destPath, overwrite: true));

            // 驗證備份完整性（備份檔大小不能為 0 或小於原始的 90%）
            var backupSize = new FileInfo(destPath).Length;
            if (backupSize == 0 || backupSize < originalSize * 0.9)
            {
                File.Delete(destPath);
                System.Diagnostics.Debug.WriteLine($"備份驗證失敗：原始 {originalSize} bytes，備份 {backupSize} bytes");
                return null;
            }

            _lastBackupDate = now.Date;
            CleanupOldBackups();
            return destPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"備份失敗：{ex.Message}");
            return null;
        }
    }

    // 備份保留天數（超過此天數的舊備份檔案將自動刪除）
    private const int BackupRetentionDays = 30;

    private void CleanupOldBackups()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-BackupRetentionDays);
            foreach (var file in Directory.GetFiles(BackupFolder, "TempleLamp_*.db"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清理舊備份失敗：{ex.Message}");
        }
    }

    private async Task RunBackupLoopAsync(CancellationToken ct)
    {
        // 啟動時補備份：若今天還未備份且已過備份時間（例如程式重啟、電腦重開機）
        var startNow = DateTime.Now;
        if (_lastBackupDate != startNow.Date && startNow.Hour >= BackupHour)
        {
            await BackupNowAsync();
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 每分鐘檢查一次
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            var now = DateTime.Now;

            // 今天已備份過就跳過
            if (_lastBackupDate == now.Date)
                continue;

            // 備份時間窗口：19:00 ~ 19:09（給 10 分鐘重試機會，防止備份失敗後整天跳過）
            if (now.Hour == BackupHour && now.Minute < 10)
            {
                await BackupNowAsync();
            }
        }
    }
}
