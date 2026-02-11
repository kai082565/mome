using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class SyncQueueService : ISyncQueueService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseService _supabaseService;
    private const int MaxRetries = 3;

    public SyncQueueService(AppDbContext context, ISupabaseService supabaseService)
    {
        _context = context;
        _supabaseService = supabaseService;
    }

    public async Task EnqueueAsync<T>(T entity, SyncOperation operation) where T : class
    {
        var entityType = GetEntityType<T>();
        var entityId = GetEntityId(entity);

        var queueItem = new SyncQueueItem
        {
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            JsonData = operation != SyncOperation.Delete
                ? JsonSerializer.Serialize(entity)
                : null,
            CreatedAt = DateTime.Now
        };

        _context.SyncQueue.Add(queueItem);
        await _context.SaveChangesAsync();
    }

    public async Task<SyncQueueResult> ProcessQueueAsync()
    {
        var result = new SyncQueueResult();

        if (!await IsOnlineAsync())
        {
            result.Errors.Add("無網路連線");
            return result;
        }

        var pendingItems = await _context.SyncQueue
            .Where(q => q.RetryCount < MaxRetries)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();

        result.TotalItems = pendingItems.Count;

        foreach (var item in pendingItems)
        {
            try
            {
                await ProcessItemAsync(item);
                _context.SyncQueue.Remove(item);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                item.RetryCount++;
                item.LastError = ex.Message;
                result.FailedCount++;
                result.Errors.Add($"{item.EntityType}/{item.EntityId}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _context.SyncQueue
            .Where(q => q.RetryCount < MaxRetries)
            .CountAsync();
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await _supabaseService.TestConnectionAsync();
        }
        catch
        {
            return false;
        }
    }

    private async Task ProcessItemAsync(SyncQueueItem item)
    {
        switch (item.EntityType)
        {
            case SyncEntityType.Customer:
                await ProcessCustomerAsync(item);
                break;
            case SyncEntityType.LampOrder:
                await ProcessLampOrderAsync(item);
                break;
        }
    }

    private async Task ProcessCustomerAsync(SyncQueueItem item)
    {
        switch (item.Operation)
        {
            case SyncOperation.Insert:
            case SyncOperation.Update:
                var customer = JsonSerializer.Deserialize<Customer>(item.JsonData!);
                if (customer != null)
                    await _supabaseService.UpsertCustomerAsync(customer);
                break;
            case SyncOperation.Delete:
                await _supabaseService.DeleteCustomerAsync(Guid.Parse(item.EntityId));
                break;
        }
    }

    private async Task ProcessLampOrderAsync(SyncQueueItem item)
    {
        switch (item.Operation)
        {
            case SyncOperation.Insert:
            case SyncOperation.Update:
                var order = JsonSerializer.Deserialize<LampOrder>(item.JsonData!);
                if (order != null)
                    await _supabaseService.UpsertLampOrderAsync(order);
                break;
            case SyncOperation.Delete:
                await _supabaseService.DeleteLampOrderAsync(Guid.Parse(item.EntityId));
                break;
        }
    }

    private static SyncEntityType GetEntityType<T>() where T : class
    {
        return typeof(T).Name switch
        {
            nameof(Customer) => SyncEntityType.Customer,
            nameof(LampOrder) => SyncEntityType.LampOrder,
            _ => throw new ArgumentException($"不支援的實體類型：{typeof(T).Name}")
        };
    }

    private static string GetEntityId<T>(T entity) where T : class
    {
        return entity switch
        {
            Customer c => c.Id.ToString(),
            LampOrder o => o.Id.ToString(),
            _ => throw new ArgumentException("無法取得實體 ID")
        };
    }
}
