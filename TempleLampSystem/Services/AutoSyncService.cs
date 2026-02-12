using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class AutoSyncService : IAutoSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _timer;
    private bool _isRunning;
    private bool _isSyncing; // 防止同步重疊執行
    private bool _lastOnlineStatus;
    private DateTime? _lastSyncTime;
    private int _syncCycleCount;

    // 每 20 次增量同步（約 10 分鐘）做一次刪除同步
    private const int DeleteSyncInterval = 20;
    // 每 6 次增量同步（約 3 分鐘）清理一次 DbContext 記憶體
    private const int MemoryCleanupInterval = 6;

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
        // 防止上一次同步還沒結束就觸發下一次（網路慢時會發生）
        if (_isSyncing) return;

        try
        {
            _isSyncing = true;
            await CheckAndSyncAsync();
        }
        catch
        {
            // 防止 Timer 例外導致閃退
        }
        finally
        {
            _isSyncing = false;
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
                _syncCycleCount++;

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

                // 增量上傳本地資料到雲端
                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = "正在同步資料..."
                });

                var uploadResult = await supabaseService.SyncToCloudAsync(_lastSyncTime);
                var totalUploaded = uploadResult.CustomersUploaded + uploadResult.OrdersUploaded;

                // 增量從雲端拉取最新資料到本地
                var syncResult = await supabaseService.SyncFromCloudAsync(_lastSyncTime);
                var totalDownloaded = syncResult.CustomersDownloaded + syncResult.OrdersDownloaded;

                // 記錄本次同步時間，下次只同步此時間之後的變動
                _lastSyncTime = DateTime.Now;

                // 定期執行刪除同步（比對雲端 ID，刪除本地多餘的資料）
                var totalDeleted = 0;
                if (_syncCycleCount % DeleteSyncInterval == 0)
                {
                    totalDeleted = await PerformDeleteSyncAsync(scope.ServiceProvider);
                }

                // 顯示同步結果
                var message = "已連線（資料已同步）";
                if (totalUploaded > 0 || totalDownloaded > 0 || totalDeleted > 0)
                {
                    var parts = new List<string>();
                    if (totalUploaded > 0) parts.Add($"上傳 {totalUploaded} 筆");
                    if (totalDownloaded > 0) parts.Add($"下載 {totalDownloaded} 筆");
                    if (totalDeleted > 0) parts.Add($"清理 {totalDeleted} 筆");
                    message = $"已同步：{string.Join("、", parts)}";
                }

                SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                {
                    IsOnline = true,
                    PendingCount = pendingCount,
                    Message = message
                });
            }

            // 定期清理 DbContext 記憶體（不管線上離線都要做）
            if (_syncCycleCount % MemoryCleanupInterval == 0)
            {
                CleanupMemory();
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

    /// <summary>
    /// 比對雲端 ID，刪除本地有但雲端沒有的資料（被其他電腦刪除的）
    /// </summary>
    private async Task<int> PerformDeleteSyncAsync(IServiceProvider scopeProvider)
    {
        var deleted = 0;
        try
        {
            var supabaseService = scopeProvider.GetRequiredService<ISupabaseService>();
            var context = scopeProvider.GetRequiredService<AppDbContext>();

            // 取得雲端所有 ID
            var cloudCustomerIds = await supabaseService.GetAllCloudCustomerIdsAsync();
            var cloudOrderIds = await supabaseService.GetAllCloudLampOrderIdsAsync();

            // 雲端回傳空集合表示查詢可能失敗，跳過刪除以免誤刪
            if (cloudCustomerIds.Count == 0 && cloudOrderIds.Count == 0)
                return 0;

            // 取得 SyncQueue 中待上傳的 ID（這些是本地新增但還沒上傳的，不能刪）
            var pendingUploadIds = await context.SyncQueue
                .Where(q => q.Operation != SyncOperation.Delete)
                .Select(q => q.EntityId)
                .ToListAsync();
            var pendingUploadSet = pendingUploadIds.ToHashSet();

            // 比對訂單（先刪訂單再刪客戶，避免外鍵衝突）
            if (cloudOrderIds.Count > 0)
            {
                var localOrders = await context.LampOrders
                    .Select(o => new { o.Id })
                    .ToListAsync();

                foreach (var local in localOrders)
                {
                    var idStr = local.Id.ToString();
                    if (!cloudOrderIds.Contains(idStr) && !pendingUploadSet.Contains(idStr))
                    {
                        var entity = await context.LampOrders.FindAsync(local.Id);
                        if (entity != null)
                        {
                            context.LampOrders.Remove(entity);
                            deleted++;
                        }
                    }
                }
            }

            // 比對客戶
            if (cloudCustomerIds.Count > 0)
            {
                var localCustomers = await context.Customers
                    .Select(c => new { c.Id })
                    .ToListAsync();

                foreach (var local in localCustomers)
                {
                    var idStr = local.Id.ToString();
                    if (!cloudCustomerIds.Contains(idStr) && !pendingUploadSet.Contains(idStr))
                    {
                        // 先確認此客戶沒有本地訂單（可能是剛建的）
                        var hasLocalOrders = await context.LampOrders
                            .AnyAsync(o => o.CustomerId == local.Id);
                        if (!hasLocalOrders)
                        {
                            var entity = await context.Customers.FindAsync(local.Id);
                            if (entity != null)
                            {
                                context.Customers.Remove(entity);
                                deleted++;
                            }
                        }
                    }
                }
            }

            if (deleted > 0)
            {
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刪除同步失敗：{ex.Message}");
        }

        return deleted;
    }

    /// <summary>
    /// 清理 DbContext 的 Change Tracker，防止長時間執行記憶體持續增長
    /// </summary>
    private void CleanupMemory()
    {
        try
        {
            // 清理根容器的 DbContext（被 ViewModel 和 Service 使用的那個）
            var rootContext = _serviceProvider.GetRequiredService<AppDbContext>();
            rootContext.ChangeTracker.Clear();
        }
        catch
        {
            // 清理失敗不影響運作
        }
    }
}
