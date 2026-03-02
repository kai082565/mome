using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ILampOrderRepository : IRepository<LampOrder>
{
    Task<List<LampOrder>> GetByCustomerIdAsync(Guid customerId);
    Task<List<LampOrder>> GetExpiringOrdersAsync(int daysBeforeExpiry);
}
