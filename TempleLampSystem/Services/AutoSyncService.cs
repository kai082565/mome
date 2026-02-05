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

            if (isOnline && pendingCount > 0)
            {
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = $"正在同步 {pendingCount} 筆資料..."
                });

                var result = await syncQueueService.ProcessQueueAsync();

                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = result.FailedCount,
                    Message = result.SuccessCount > 0
                        ? $"已同步 {result.SuccessCount} 筆資料"
                        : "同步完成"
                });
            }
            else if (isOnline && pendingCount == 0)
            {
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = 0,
                    Message = "已連線"
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
