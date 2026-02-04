using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public class LampOrderRepository : RepositoryBase<LampOrder>, ILampOrderRepository
{
    public LampOrderRepository(AppDbContext context) : base(context) { }

    public async Task<List<LampOrder>> GetByCustomerIdAsync(Guid customerId)
    {
        return await _dbSet
            .Include(o => o.Lamp)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.Year)
            .ThenBy(o => o.Lamp.LampName)
            .ToListAsync();
    }

    public async Task<List<LampOrder>> GetByYearAsync(int year)
    {
        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.Year == year)
            .OrderBy(o => o.Customer.Name)
            .ToListAsync();
    }

    public async Task<List<LampOrder>> GetByYearAndLampAsync(int year, int lampId)
    {
        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.Year == year && o.LampId == lampId)
            .OrderBy(o => o.Customer.Name)
            .ToListAsync();
    }

    public async Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry)
    {
        var targetDate = DateTime.UtcNow.AddDays(daysBeforeExpiry);
        var today = DateTime.UtcNow;

        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.EndDate >= today && o.EndDate <= targetDate)
            .OrderBy(o => o.EndDate)
            .ToListAsync();
    }

    public async Task<List<LampOrder>> GetExpiredOrdersAsync()
    {
        var today = DateTime.UtcNow;

        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.EndDate < today)
            .OrderByDescending(o => o.EndDate)
            .ToListAsync();
    }

    public async Task<LampOrder?> GetWithDetailsAsync(Guid orderId)
    {
        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public override async Task<LampOrder> AddAsync(LampOrder entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        return await base.AddAsync(entity);
    }

    public override async Task UpdateAsync(LampOrder entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await base.UpdateAsync(entity);
    }
}
