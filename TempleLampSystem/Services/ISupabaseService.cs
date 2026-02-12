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

    // 雲端查詢
    bool IsConfigured { get; }
    Task<bool> HasActiveOrderAsync(Guid customerId, int lampId);

    // 客戶編號
    Task<string?> GetMaxCustomerCodeAsync();

    // 名額查詢
    Task<int> GetCloudOrderCountAsync(int lampId, int year);

    // 同步（支援增量）
    Task<SyncResult> SyncToCloudAsync(DateTime? since = null);
    Task<SyncResult> SyncFromCloudAsync(DateTime? since = null);

    // 刪除同步：取得雲端所有 ID，用於比對本地是否有被其他機器刪除的資料
    Task<HashSet<string>> GetAllCloudCustomerIdsAsync();
    Task<HashSet<string>> GetAllCloudLampOrderIdsAsync();
}

public class SyncResult
{
    public bool Success { get; set; }
    public int CustomersUploaded { get; set; }
    public int CustomersDownloaded { get; set; }
    public int OrdersUploaded { get; set; }
    public int OrdersDownloaded { get; set; }
    public int CustomersDeleted { get; set; }
    public int OrdersDeleted { get; set; }
    public string? ErrorMessage { get; set; }
}
