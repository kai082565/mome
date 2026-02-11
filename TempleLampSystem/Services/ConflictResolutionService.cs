using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class ConflictResolutionService : IConflictResolutionService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseService _supabaseService;

    public ConflictResolutionService(AppDbContext context, ISupabaseService supabaseService)
    {
        _context = context;
        _supabaseService = supabaseService;
    }

    public async Task<List<SyncConflict>> DetectConflictsAsync()
    {
        var conflicts = new List<SyncConflict>();

        var localCustomers = await _context.Customers.ToListAsync();
        var remoteCustomers = await _supabaseService.GetAllCustomersAsync();

        foreach (var local in localCustomers)
        {
            var remote = remoteCustomers.FirstOrDefault(r => r.Id == local.Id);
            if (remote != null &&
                local.UpdatedAt != remote.UpdatedAt &&
                Math.Abs((local.UpdatedAt - remote.UpdatedAt).TotalSeconds) > 1)
            {
                var existingConflict = await _context.SyncConflicts
                    .FirstOrDefaultAsync(c => c.EntityId == local.Id.ToString() && c.Resolution == null);

                if (existingConflict == null)
                {
                    conflicts.Add(new SyncConflict
                    {
                        EntityType = SyncEntityType.Customer,
                        EntityId = local.Id.ToString(),
                        LocalData = JsonSerializer.Serialize(local),
                        RemoteData = JsonSerializer.Serialize(remote),
                        LocalUpdatedAt = local.UpdatedAt,
                        RemoteUpdatedAt = remote.UpdatedAt
                    });
                }
            }
        }

        if (conflicts.Any())
        {
            _context.SyncConflicts.AddRange(conflicts);
            await _context.SaveChangesAsync();
        }

        return conflicts;
    }

    public async Task<int> GetUnresolvedConflictCountAsync()
    {
        return await _context.SyncConflicts.CountAsync(c => c.Resolution == null);
    }

    public async Task ResolveConflictAsync(int conflictId, ConflictResolution resolution)
    {
        var conflict = await _context.SyncConflicts.FindAsync(conflictId);
        if (conflict == null) return;

        switch (resolution)
        {
            case ConflictResolution.UseLocal:
                await ApplyLocalDataAsync(conflict);
                break;
            case ConflictResolution.UseRemote:
                await ApplyRemoteDataAsync(conflict);
                break;
            case ConflictResolution.Merge:
                await MergeDataAsync(conflict);
                break;
        }

        conflict.Resolution = resolution;
        conflict.ResolvedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task AutoResolveAllAsync(ConflictResolution defaultResolution = ConflictResolution.Merge)
    {
        var unresolvedConflicts = await _context.SyncConflicts
            .Where(c => c.Resolution == null)
            .ToListAsync();

        foreach (var conflict in unresolvedConflicts)
        {
            await ResolveConflictAsync(conflict.Id, defaultResolution);
        }
    }

    private async Task ApplyLocalDataAsync(SyncConflict conflict)
    {
        if (conflict.EntityType == SyncEntityType.Customer)
        {
            var customer = JsonSerializer.Deserialize<Customer>(conflict.LocalData);
            if (customer != null)
            {
                customer.UpdatedAt = DateTime.Now;
                await _supabaseService.UpsertCustomerAsync(customer);
            }
        }
    }

    private async Task ApplyRemoteDataAsync(SyncConflict conflict)
    {
        if (conflict.EntityType == SyncEntityType.Customer)
        {
            var remote = JsonSerializer.Deserialize<Customer>(conflict.RemoteData);
            if (remote != null)
            {
                var local = await _context.Customers.FindAsync(Guid.Parse(conflict.EntityId));
                if (local != null)
                {
                    local.Name = remote.Name;
                    local.Phone = remote.Phone;
                    local.Mobile = remote.Mobile;
                    local.Address = remote.Address;
                    local.Note = remote.Note;
                    local.UpdatedAt = remote.UpdatedAt;
                    await _context.SaveChangesAsync();
                }
            }
        }
    }

    private async Task MergeDataAsync(SyncConflict conflict)
    {
        if (conflict.LocalUpdatedAt > conflict.RemoteUpdatedAt)
        {
            await ApplyLocalDataAsync(conflict);
        }
        else
        {
            await ApplyRemoteDataAsync(conflict);
        }
    }
}
