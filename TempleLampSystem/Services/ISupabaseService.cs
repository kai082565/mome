using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface ISupabaseService
{
    Task<bool> TestConnectionAsync();

    // Customer
    Task<Customer?> GetCustomerAsync(Guid id);
    Task<List<Customer>> GetAllCustomersAsync();
    Task<Customer> UpsertCustomerAsync(Customer customer);
    Task DeleteCustomerAsync(Guid id);

    // Lamp
    Task<List<Lamp>> GetAllLampsAsync();
    Task<Lamp> UpsertLampAsync(Lamp lamp);

    // LampOrder
    Task<LampOrder?> GetLampOrderAsync(Guid id);
    Task<List<LampOrder>> GetLampOrdersByCustomerAsync(Guid customerId);
    Task<LampOrder> UpsertLampOrderAsync(LampOrder order);
    Task DeleteLampOrderAsync(Guid id);

    // 同步
    Task<SyncResult> SyncToCloudAsync();
    Task<SyncResult> SyncFromCloudAsync();
}

public class SyncResult
{
    public bool Success { get; set; }
    public int CustomersUploaded { get; set; }
    public int CustomersDownloaded { get; set; }
    public int OrdersUploaded { get; set; }
    public int OrdersDownloaded { get; set; }
    public string? ErrorMessage { get; set; }
}
