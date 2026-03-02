using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ILampRepository : IRepository<Lamp>
{
    Task<List<Lamp>> GetAllOrderedAsync();
}
