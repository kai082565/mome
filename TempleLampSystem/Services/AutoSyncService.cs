using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace TempleLampSystem.Services;

public class AutoSyncService : IAutoSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _timer;
    private bool _isRunning;
    private bool _lastOnlineStatus;

    public bool IsRunning => _isRunning;
    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;

    public AutoSyncService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _timer.Tick += OnTimerTick;
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            await CheckAndSyncAsync();
        }
        catch
        {
            // 防止 Timer 例外導致閃退
        }
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _timer.Start();
        _ = SafeCheckAndSyncAsync();
    }

    private async Task SafeCheckAndSyncAsync()
    {
        try
        {
            await CheckAndSyncAsync();
        }
        catch
        {
            // 防止啟動時的同步錯誤導致閃退
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _timer.Stop();
    }

    private async Task CheckAndSyncAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncQueueService = scope.ServiceProvider.GetRequiredService<ISyncQueueService>();
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();

            var isOnline = await syncQueueService.IsOnlineAsync();
            var pendingCount = await syncQueueService.GetPendingCountAsync();

            if (isOnline != _lastOnlineStatus)
            {
                _lastOnlineStatus = isOnline;
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = isOnline,
                    PendingCount = pendingCount,
                    Message = isOnline ? "已連線" : "離線模式"
                });
            }

            if (isOnline)
            {
                // 先處理待上傳的佇列
                if (pendingCount > 0)
                {
                    SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                    {
                        IsOnline = true,
                        PendingCount = pendingCount,
                        Message = $"正在上傳 {pendingCount} 筆資料..."
                    });

                    var result = await syncQueueService.ProcessQueueAsync();
                    pendingCount = result.FailedCount;
                }

                // 自動上傳本地資料到雲端（Upsert 會自動判斷新增或更新）
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = "正在上傳本地資料..."
                });

                var uploadResult = await supabaseService.SyncToCloudAsync();
                var totalUploaded = uploadResult.CustomersUploaded + uploadResult.OrdersUploaded;

                // 從雲端拉取最新資料到本地
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = "正在下載雲端資料..."
                });

                var syncResult = await supabaseService.SyncFromCloudAsync();
                var totalDownloaded = syncResult.CustomersDownloaded + syncResult.OrdersDownloaded;

                // 顯示同步結果
                var message = "已連線（資料已同步）";
                if (totalUploaded > 0 || totalDownloaded > 0)
                {
                    var parts = new List<string>();
                    if (totalUploaded > 0) parts.Add($"上傳 {totalUploaded} 筆");
                    if (totalDownloaded > 0) parts.Add($"下載 {totalDownloaded} 筆");
                    message = $"已同步：{string.Join("、", parts)}";
                }

                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = message
                });
            }
        }
        catch (Exception ex)
        {
            SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
            {
                IsOnline = false,
                Message = $"同步錯誤：{ex.Message}"
            });
        }
    }
}
