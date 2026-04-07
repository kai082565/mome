using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class AutoSyncService : IAutoSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _isSyncing; // 防止同步重疊執行
    private bool _isPaused;  // 匯入期間暫停同步
    private bool _lastOnlineStatus;
    private DateTime? _lastSyncTime;
    private int _syncCycleCount;
    private int _totalCycleCount; // 含離線次數，用於記憶體清理

    // 每 20 次線上同步（約 10 分鐘）做一次刪除同步
    private const int DeleteSyncInterval = 20;
    // 每 12 次循環（約 6 分鐘，含離線）清理一次 DbContext 記憶體
    private const int MemoryCleanupInterval = 12;

    public bool IsRunning => _isRunning;
    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;

    public AutoSyncService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
        _lastSyncTime = DateTime.Now; // 跳過匯入期間的變動，避免重複上傳
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _ = RunSyncLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 在背景線程執行同步迴圈，完全不佔用 UI 線程
    /// </summary>
    private async Task RunSyncLoopAsync(CancellationToken ct)
    {
        // 啟動時先做一次同步
        await SafeCheckAndSyncAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (_isSyncing || _isPaused) continue;

            try
            {
                _isSyncing = true;
                await CheckAndSyncAsync();
            }
            catch
            {
                // 防止同步例外導致閃退
            }
            finally
            {
                _isSyncing = false;
            }
        }
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

    private async Task CheckAndSyncAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncQueueService = scope.ServiceProvider.GetRequiredService<ISyncQueueService>();
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();

            var isOnline = await syncQueueService.IsOnlineAsync();
            var pendingCount = await syncQueueService.GetPendingCountAsync();

            _totalCycleCount++; // 每次循環都計數，含離線

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

                // 若同步失敗，顯示錯誤訊息（便於診斷）
                if (!syncResult.Success && !string.IsNullOrEmpty(syncResult.ErrorMessage))
                {
                    SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
                    {
                        IsOnline = true,
                        PendingCount = pendingCount,
                        Message = $"同步失敗：{syncResult.ErrorMessage}"
                    });
                    return;
                }

                // 若有新增客戶，重新執行編號指派（修補雲端 null/重複 code）
                if (syncResult.CustomersDownloaded > 0)
                {
                    using var codeScope = _serviceProvider.CreateScope();
                    var ctx = codeScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    DbInitializer.AssignCustomerCodes(ctx);
                }

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
                    Message = message,
                    HasNewData = totalDownloaded > 0
                });
            }

            // 定期清理 DbContext 記憶體（不管線上離線都要做，使用獨立計數器）
            if (_totalCycleCount % MemoryCleanupInterval == 0)
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

            // 取得本機資料數量
            var localCustomerCount = await context.Customers.CountAsync();
            var localOrderCount = await context.LampOrders.CountAsync();

            // 取得雲端所有 ID
            var cloudCustomerIds = await supabaseService.GetAllCloudCustomerIdsAsync();
            var cloudOrderIds = await supabaseService.GetAllCloudLampOrderIdsAsync();

            // 安全防護 1：兩個查詢都必須有回傳值才繼續（任一為空表示查詢可能失敗）
            if (cloudCustomerIds.Count == 0 || cloudOrderIds.Count == 0)
                return 0;

            // 安全防護 2：雲端筆數不能遠少於本機（防止網路問題導致部分回傳）
            // 雲端至少要有本機 80% 的資料才允許刪除
            if (cloudCustomerIds.Count < localCustomerCount * 0.8 ||
                cloudOrderIds.Count < localOrderCount * 0.8)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"刪除同步跳過：雲端資料筆數異常偏少（客戶 {cloudCustomerIds.Count}/{localCustomerCount}，訂單 {cloudOrderIds.Count}/{localOrderCount}）");
                return 0;
            }

            // 取得 SyncQueue 中待上傳的 ID（這些是本地新增但還沒上傳的，不能刪）
            var pendingUploadIds = await context.SyncQueue
                .Where(q => q.Operation != SyncOperation.Delete)
                .Select(q => q.EntityId)
                .ToListAsync();
            var pendingUploadSet = pendingUploadIds.ToHashSet();

            // 比對訂單（先刪訂單再刪客戶，避免外鍵衝突）
            var localOrderIds = await context.LampOrders
                .Select(o => o.Id)
                .ToListAsync();

            var orderIdsToDelete = localOrderIds
                .Where(id =>
                {
                    var idStr = id.ToString();
                    return !cloudOrderIds.Contains(idStr) && !pendingUploadSet.Contains(idStr);
                })
                .ToList();

            // 安全防護 3：單次刪除超過 10 筆或超過本機總量 10%，拒絕執行
            if (orderIdsToDelete.Count > 10 || orderIdsToDelete.Count > localOrderCount * 0.1)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"刪除同步跳過：準備刪除 {orderIdsToDelete.Count} 筆訂單，超過安全閾值");
                return 0;
            }

            if (orderIdsToDelete.Count > 0)
            {
                await context.LampOrders
                    .Where(o => orderIdsToDelete.Contains(o.Id))
                    .ExecuteDeleteAsync();
                deleted += orderIdsToDelete.Count;
            }

            // 比對客戶
            var localCustomerIds = await context.Customers
                .Select(c => c.Id)
                .ToListAsync();

            var customerIdsToDelete = localCustomerIds
                .Where(id =>
                {
                    var idStr = id.ToString();
                    return !cloudCustomerIds.Contains(idStr) && !pendingUploadSet.Contains(idStr);
                })
                .ToList();

            // 安全防護 3（客戶）：單次刪除超過 5 筆或超過本機總量 10%，拒絕執行
            if (customerIdsToDelete.Count > 5 || customerIdsToDelete.Count > localCustomerCount * 0.1)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"刪除同步跳過：準備刪除 {customerIdsToDelete.Count} 筆客戶，超過安全閾值");
                return 0;
            }

            if (customerIdsToDelete.Count > 0)
            {
                // 只刪除沒有本地訂單的客戶
                var customerIdsWithOrders = await context.LampOrders
                    .Where(o => customerIdsToDelete.Contains(o.CustomerId))
                    .Select(o => o.CustomerId)
                    .Distinct()
                    .ToListAsync();

                var safeToDelete = customerIdsToDelete
                    .Where(id => !customerIdsWithOrders.Contains(id))
                    .ToList();

                if (safeToDelete.Count > 0)
                {
                    await context.Customers
                        .Where(c => safeToDelete.Contains(c.Id))
                        .ExecuteDeleteAsync();
                    deleted += safeToDelete.Count;
                }
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
