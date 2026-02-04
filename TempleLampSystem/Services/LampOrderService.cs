using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.Services;

public class LampOrderService : ILampOrderService
{
    private readonly AppDbContext _context;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILampOrderRepository _lampOrderRepository;
    private readonly ISyncQueueService? _syncQueueService;

    public LampOrderService(
        AppDbContext context,
        ICustomerRepository customerRepository,
        ILampOrderRepository lampOrderRepository,
        ISyncQueueService? syncQueueService = null)
    {
        _context = context;
        _customerRepository = customerRepository;
        _lampOrderRepository = lampOrderRepository;
        _syncQueueService = syncQueueService;
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

        var relatedCustomerIds = await GetRelatedCustomerIdsAsync(customer);

        var today = DateTime.UtcNow.Date;
        var hasActiveOrder = await _context.LampOrders
            .AnyAsync(o => relatedCustomerIds.Contains(o.CustomerId) &&
                          o.LampId == lampId &&
                          o.EndDate >= today);

        if (hasActiveOrder)
            return "該客戶（或同電話客戶）已有未過期的此燈種點燈紀錄";

        return null;
    }

    public async Task<LampOrder> CreateLampOrderAsync(Guid customerId, int lampId, decimal price)
    {
        var reason = await GetCannotOrderReasonAsync(customerId, lampId);
        if (reason != null)
            throw new InvalidOperationException(reason);

        var today = DateTime.UtcNow.Date;
        var order = new LampOrder
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            LampId = lampId,
            StartDate = today,
            EndDate = today.AddYears(1).AddDays(-1),
            Year = today.Year,
            Price = price,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _lampOrderRepository.AddAsync(order);

        if (_syncQueueService != null)
        {
            try
            {
                await _syncQueueService.EnqueueAsync(order, SyncOperation.Insert);
            }
            catch
            {
                // 同步佇列失敗不影響本地操作
            }
        }

        return order;
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
