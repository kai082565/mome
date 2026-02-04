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
        // 載入所有客戶
        var allCustomers = await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .ToListAsync();

        // 如果沒有搜尋條件，返回所有客戶
        if (string.IsNullOrWhiteSpace(phone))
        {
            return allCustomers.OrderBy(c => c.Name).ToList();
        }

        // 移除搜尋字串中的所有非數字字符
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(digitsOnly))
        {
            return allCustomers.OrderBy(c => c.Name).ToList();
        }

        // 在記憶體中過濾 - 同時支援原始格式和純數字格式
        var results = new List<Customer>();
        foreach (var c in allCustomers)
        {
            // 檢查原始電話號碼（帶-）
            bool phoneMatch = !string.IsNullOrEmpty(c.Phone) &&
                (c.Phone.Contains(phone) || c.Phone.Replace("-", "").Contains(digitsOnly));

            bool mobileMatch = !string.IsNullOrEmpty(c.Mobile) &&
                (c.Mobile.Contains(phone) || c.Mobile.Replace("-", "").Contains(digitsOnly));

            if (phoneMatch || mobileMatch)
            {
                results.Add(c);
            }
        }

        return results.OrderBy(c => c.Name).ToList();
    }

    public override async Task UpdateAsync(Customer entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await base.UpdateAsync(entity);
    }
}
