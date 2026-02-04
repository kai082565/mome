using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public class LampRepository : RepositoryBase<Lamp>, ILampRepository
{
    public LampRepository(AppDbContext context) : base(context) { }

    public async Task<Lamp?> GetByCodeAsync(string lampCode)
    {
        return await _dbSet.FirstOrDefaultAsync(l => l.LampCode == lampCode);
    }

    public async Task<List<Lamp>> GetAllOrderedAsync()
    {
        return await _dbSet.OrderBy(l => l.Id).ToListAsync();
    }
}
