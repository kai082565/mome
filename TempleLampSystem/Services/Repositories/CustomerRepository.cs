using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public class CustomerRepository : RepositoryBase<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public async Task<Customer?> GetWithOrdersAsync(Guid customerId)
    {
        return await _dbSet
            .Include(c => c.LampOrders)
                .ThenInclude(o => o.Lamp)
            .FirstOrDefaultAsync(c => c.Id == customerId);
    }

    public async Task<List<Customer>> SearchByPhoneWithOrdersAsync(string searchText)
    {
        // 無搜尋條件時回傳最近更新的 200 筆
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.LampOrders)
                    .ThenInclude(o => o.Lamp)
                .OrderByDescending(c => c.UpdatedAt)
                .Take(200)
                .ToListAsync();
        }

        var keyword = searchText.Trim();
        var digitsOnly = new string(keyword.Where(char.IsDigit).ToArray());
        var hasDigits = digitsOnly.Length > 0;

        IQueryable<Customer> query;

        if (hasDigits)
        {
            // 搜尋包含數字：比對電話（含去除 - 的版本）、姓名、編號
            query = _dbSet
                .AsNoTracking()
                .Include(c => c.LampOrders)
                    .ThenInclude(o => o.Lamp)
                .Where(c =>
                    c.Name.Contains(keyword) ||
                    (c.CustomerCode != null && c.CustomerCode.Contains(keyword)) ||
                    (c.Phone != null && (c.Phone.Contains(keyword) || c.Phone.Replace("-", "").Contains(digitsOnly))) ||
                    (c.Mobile != null && (c.Mobile.Contains(keyword) || c.Mobile.Replace("-", "").Contains(digitsOnly))));
        }
        else
        {
            // 純文字搜尋：比對姓名、編號
            query = _dbSet
                .AsNoTracking()
                .Include(c => c.LampOrders)
                    .ThenInclude(o => o.Lamp)
                .Where(c =>
                    c.Name.Contains(keyword) ||
                    (c.CustomerCode != null && c.CustomerCode.Contains(keyword)));
        }

        var matched = await query
            .OrderBy(c => c.Name)
            .Take(200)
            .ToListAsync();

        // 同住址視為一戶：符合搜尋條件的客戶，其他住在同一個地址的人也一併顯示
        var addresses = matched
            .Where(c => !string.IsNullOrWhiteSpace(c.Address))
            .Select(c => c.Address!)
            .Distinct()
            .ToList();

        if (addresses.Count > 0)
        {
            var matchedIds = matched.Select(c => c.Id).ToHashSet();
            var sameAddress = await _dbSet
                .AsNoTracking()
                .Include(c => c.LampOrders)
                    .ThenInclude(o => o.Lamp)
                .Where(c => c.Address != null && addresses.Contains(c.Address) && !matchedIds.Contains(c.Id))
                .ToListAsync();

            matched.AddRange(sameAddress);
        }

        return matched.OrderBy(c => c.Name).ToList();
    }

    public override async Task UpdateAsync(Customer entity)
    {
        entity.UpdatedAt = DateTime.Now;
        await base.UpdateAsync(entity);
    }

    public async Task<List<Customer>> GetFamilyMembersAsync(Guid customerId)
    {
        var customer = await _dbSet.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null)
            return new List<Customer>();

        var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);
        var hasMobile = !string.IsNullOrWhiteSpace(customer.Mobile);

        if (!hasPhone && !hasMobile)
            return new List<Customer>();

        return await _dbSet
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(c =>
                (hasPhone && c.Phone == phone) ||
                (hasMobile && c.Mobile == mobile))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Customer?> FindDuplicateAsync(string name, string? phone, string? address, Guid? excludeId = null)
    {
        var trimmedName = name.Trim();
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasAddress = !string.IsNullOrWhiteSpace(address);

        if (trimmedName.Length == 0 || (!hasPhone && !hasAddress))
            return null;

        return await _dbSet
            .AsNoTracking()
            .Where(c => c.Id != excludeId && c.Name == trimmedName &&
                ((hasPhone && (c.Phone == phone || c.Mobile == phone)) ||
                 (hasAddress && c.Address == address)))
            .FirstOrDefaultAsync();
    }

    public async Task<string> GetNextCustomerCodeAsync()
    {
        // 用 SQL 直接取最大值，不載入所有記錄
        var maxCode = await _dbSet
            .Where(c => c.CustomerCode != null && c.CustomerCode != "")
            .Select(c => c.CustomerCode!)
            .MaxAsync(c => (string?)c);

        var maxNum = 0;
        if (maxCode != null && int.TryParse(maxCode, out var n))
            maxNum = n;

        return (maxNum + 1).ToString("D6");
    }
}
