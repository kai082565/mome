using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ILampRepository : IRepository<Lamp>
{
    Task<Lamp?> GetByCodeAsync(string lampCode);
    Task<List<Lamp>> GetAllOrderedAsync();
}
