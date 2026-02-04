using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<List<Customer>> SearchByPhoneAsync(string phone);
    Task<List<Customer>> SearchByNameAsync(string name);
    Task<Customer?> GetWithOrdersAsync(Guid customerId);
    Task<List<Customer>> SearchByPhoneWithOrdersAsync(string phone);
}
