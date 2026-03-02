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

    public async Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry)
    {
        var targetDate = DateTime.Now.AddDays(daysBeforeExpiry);
        var today = DateTime.Now;

        return await _dbSet
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.EndDate >= today && o.EndDate <= targetDate)
            .OrderBy(o => o.EndDate)
            .ToListAsync();
    }

    public override async Task<LampOrder> AddAsync(LampOrder entity)
    {
        entity.CreatedAt = DateTime.Now;
        entity.UpdatedAt = DateTime.Now;
        return await base.AddAsync(entity);
    }

    public override async Task UpdateAsync(LampOrder entity)
    {
        entity.UpdatedAt = DateTime.Now;
        await base.UpdateAsync(entity);
    }
}
