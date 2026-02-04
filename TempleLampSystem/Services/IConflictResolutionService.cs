using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface IConflictResolutionService
{
    Task<List<SyncConflict>> DetectConflictsAsync();
    Task<int> GetUnresolvedConflictCountAsync();
    Task ResolveConflictAsync(int conflictId, ConflictResolution resolution);
    Task AutoResolveAllAsync(ConflictResolution defaultResolution = ConflictResolution.Merge);
}
