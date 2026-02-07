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

    public async Task<List<Customer>> SearchByPhoneWithOrdersAsync(string searchText)
    {
        // 載入所有客戶
        var allCustomers = await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .ToListAsync();

        // 如果沒有搜尋條件，返回所有客戶
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return allCustomers.OrderBy(c => c.Name).ToList();
        }

        var keyword = searchText.Trim();

        // 移除搜尋字串中的所有非數字字符（用於電話比對）
        var digitsOnly = new string(keyword.Where(char.IsDigit).ToArray());

        // 在記憶體中過濾 - 支援姓名、電話、手機搜尋
        var results = new List<Customer>();
        foreach (var c in allCustomers)
        {
            // 檢查姓名
            bool nameMatch = c.Name.Contains(keyword);

            // 檢查電話號碼（帶-或純數字）
            bool phoneMatch = !string.IsNullOrEmpty(c.Phone) &&
                (c.Phone.Contains(keyword) ||
                 (!string.IsNullOrEmpty(digitsOnly) && c.Phone.Replace("-", "").Contains(digitsOnly)));

            // 檢查手機號碼（帶-或純數字）
            bool mobileMatch = !string.IsNullOrEmpty(c.Mobile) &&
                (c.Mobile.Contains(keyword) ||
                 (!string.IsNullOrEmpty(digitsOnly) && c.Mobile.Replace("-", "").Contains(digitsOnly)));

            // 檢查客戶編號
            bool codeMatch = !string.IsNullOrEmpty(c.CustomerCode) &&
                c.CustomerCode.Contains(keyword);

            if (nameMatch || phoneMatch || mobileMatch || codeMatch)
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

    public async Task<List<Customer>> GetFamilyMembersAsync(Guid customerId)
    {
        var customer = await _dbSet.FindAsync(customerId);
        if (customer == null)
            return new List<Customer>();

        var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);
        var hasMobile = !string.IsNullOrWhiteSpace(customer.Mobile);

        if (!hasPhone && !hasMobile)
            return new List<Customer>();

        return await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .Where(c => c.Id != customerId &&
                ((hasPhone && c.Phone == customer.Phone) ||
                 (hasMobile && c.Mobile == customer.Mobile)))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Customer>> FindByPhoneOrMobileAsync(string? phone, string? mobile)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasMobile = !string.IsNullOrWhiteSpace(mobile);

        if (!hasPhone && !hasMobile)
            return new List<Customer>();

        return await _dbSet
            .Where(c =>
                (hasPhone && c.Phone == phone) ||
                (hasMobile && c.Mobile == mobile))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<string> GetNextCustomerCodeAsync()
    {
        var allCodes = await _dbSet
            .Where(c => c.CustomerCode != null && c.CustomerCode != "")
            .Select(c => c.CustomerCode!)
            .ToListAsync();

        var maxCode = allCodes
            .Select(code => int.TryParse(code, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (maxCode + 1).ToString("D6");
    }
}
