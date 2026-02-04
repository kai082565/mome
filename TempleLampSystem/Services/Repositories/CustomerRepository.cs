using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public class CustomerRepository : RepositoryBase<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public async Task<List<Customer>> SearchByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return new List<Customer>();

        return await _dbSet
            .Where(c => (c.Phone != null && c.Phone.Contains(phone)) ||
                        (c.Mobile != null && c.Mobile.Contains(phone)))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Customer>> SearchByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new List<Customer>();

        return await _dbSet
            .Where(c => c.Name.Contains(name))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Customer?> GetWithOrdersAsync(Guid customerId)
    {
        return await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .FirstOrDefaultAsync(c => c.Id == customerId);
    }

    public async Task<List<Customer>> SearchByPhoneWithOrdersAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return new List<Customer>();

        return await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .Where(c => (c.Phone != null && c.Phone.Contains(phone)) ||
                        (c.Mobile != null && c.Mobile.Contains(phone)))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public override async Task UpdateAsync(Customer entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await base.UpdateAsync(entity);
    }
}
