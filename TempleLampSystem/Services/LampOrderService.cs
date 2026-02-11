using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Helpers;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.Services;

public class LampOrderService : ILampOrderService
{
    private readonly AppDbContext _context;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILampOrderRepository _lampOrderRepository;
    private readonly ISupabaseService _supabaseService;

    public LampOrderService(
        AppDbContext context,
        ICustomerRepository customerRepository,
        ILampOrderRepository lampOrderRepository,
        ISupabaseService supabaseService)
    {
        _context = context;
        _customerRepository = customerRepository;
        _lampOrderRepository = lampOrderRepository;
        _supabaseService = supabaseService;
    }

    public async Task<bool> CanOrderLampAsync(Guid customerId, int lampId)
    {
        var reason = await GetCannotOrderReasonAsync(customerId, lampId);
        return reason == null;
    }

    public async Task<string?> GetCannotOrderReasonAsync(Guid customerId, int lampId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
            return "找不到該客戶";

        // 優先查雲端（即時跨電腦同步），失敗才查本地
        bool hasActiveOrder;
        try
        {
            if (_supabaseService.IsConfigured)
            {
                hasActiveOrder = await _supabaseService.HasActiveOrderAsync(customerId, lampId);
            }
            else
            {
                hasActiveOrder = await CheckLocalActiveOrderAsync(customerId, lampId);
            }
        }
        catch
        {
            // 雲端查詢失敗，改查本地
            hasActiveOrder = await CheckLocalActiveOrderAsync(customerId, lampId);
        }

        if (hasActiveOrder)
            return "該客戶已有未過期的此燈種點燈紀錄";

        // 檢查年度限量
        var remaining = await GetRemainingQuotaAsync(lampId);
        if (remaining == 0)
            return "此燈種今年名額已額滿";

        return null;
    }

    private async Task<bool> CheckLocalActiveOrderAsync(Guid customerId, int lampId)
    {
        var today = DateTime.Now.Date;
        return await _context.LampOrders
            .AnyAsync(o => o.CustomerId == customerId &&
                          o.LampId == lampId &&
                          o.EndDate > today);
    }

    public async Task<LampOrder> CreateLampOrderAsync(Guid customerId, int lampId, decimal price, string? note = null)
    {
        var reason = await GetCannotOrderReasonAsync(customerId, lampId);
        if (reason != null)
            throw new InvalidOperationException(reason);

        var today = DateTime.Now.Date;
        var endDate = LunarCalendarHelper.GetLunarYearEndDate(today);
        var order = new LampOrder
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            LampId = lampId,
            StartDate = today,
            EndDate = endDate,
            Year = today.Year,
            Price = price,
            Note = note,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // 寫入本地
        await _lampOrderRepository.AddAsync(order);

        return order;
    }

    public async Task<int> GetRemainingQuotaAsync(int lampId)
    {
        var lamp = await _context.Lamps.FindAsync(lampId);
        if (lamp == null || lamp.MaxQuota <= 0)
            return -1; // -1 表示不限量

        var currentYear = DateTime.Now.Year;
        var usedCount = await _context.LampOrders
            .CountAsync(o => o.LampId == lampId && o.Year == currentYear);

        return Math.Max(0, lamp.MaxQuota - usedCount);
    }

    public async Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry = 30)
    {
        return await _lampOrderRepository.GetExpiringOrdersAsync(daysBeforeExpiry);
    }

    private async Task<List<Guid>> GetRelatedCustomerIdsAsync(Customer customer)
    {
        var query = _context.Customers.AsQueryable();

        var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);
        var hasMobile = !string.IsNullOrWhiteSpace(customer.Mobile);

        if (hasPhone || hasMobile)
        {
            query = query.Where(c =>
                (hasPhone && c.Phone == customer.Phone) ||
                (hasMobile && c.Mobile == customer.Mobile) ||
                c.Id == customer.Id);
        }
        else
        {
            query = query.Where(c => c.Id == customer.Id);
        }

        return await query.Select(c => c.Id).ToListAsync();
    }
}
