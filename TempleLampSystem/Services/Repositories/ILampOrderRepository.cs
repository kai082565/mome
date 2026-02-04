using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ILampOrderRepository : IRepository<LampOrder>
{
    Task<List<LampOrder>> GetByCustomerIdAsync(Guid customerId);
    Task<List<LampOrder>> GetByYearAsync(int year);
    Task<List<LampOrder>> GetByYearAndLampAsync(int year, int lampId);
    Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry);
    Task<List<LampOrder>> GetExpiredOrdersAsync();
    Task<LampOrder?> GetWithDetailsAsync(Guid orderId);
}
