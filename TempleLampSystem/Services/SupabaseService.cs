using Microsoft.EntityFrameworkCore;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using TempleLampSystem.Models;
using static Supabase.Postgrest.Constants;

namespace TempleLampSystem.Services;

public class SupabaseService : ISupabaseService
{
    private readonly Client? _client;
    private readonly AppDbContext _localContext;
    private readonly bool _isConfigured;

    public SupabaseService(AppDbContext localContext)
    {
        _localContext = localContext;

        var settings = AppSettings.Instance.Supabase;

        if (!string.IsNullOrEmpty(settings.Url) &&
            !string.IsNullOrEmpty(settings.AnonKey) &&
            !settings.Url.Contains("your-project"))
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            _client = new Client(settings.Url, settings.AnonKey, options);
            _isConfigured = true;
        }
        else
        {
            _isConfigured = false;
        }
    }

    public bool IsConfigured => _isConfigured;

    public async Task<bool> TestConnectionAsync()
    {
        if (!_isConfigured || _client == null)
            return false;

        try
        {
            await _client.From<SupabaseLamp>().Get();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #region Customer

    public async Task<Customer?> GetCustomerAsync(Guid id)
    {
        if (!_isConfigured || _client == null) return null;

        var idStr = id.ToString();
        var response = await _client
            .From<SupabaseCustomer>()
            .Where(c => c.Id == idStr)
            .Single();

        return response?.ToCustomer();
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        if (!_isConfigured || _client == null)
            return new List<Customer>();

        var response = await _client.From<SupabaseCustomer>().Get();
        return response.Models.Select(c => c.ToCustomer()).ToList();
    }

    public async Task<Customer> UpsertCustomerAsync(Customer customer)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        var supabaseCustomer = SupabaseCustomer.FromCustomer(customer);
        await _client.From<SupabaseCustomer>().Upsert(supabaseCustomer);
        return customer;
    }

    public async Task DeleteCustomerAsync(Guid id)
    {
        if (!_isConfigured || _client == null) return;
        var idStr = id.ToString();
        await _client.From<SupabaseCustomer>().Where(c => c.Id == idStr).Delete();
    }

    public async Task<string?> GetMaxCustomerCodeAsync()
    {
        if (!_isConfigured || _client == null) return null;

        try
        {
            // 只取 CustomerCode 欄位，按降序取第一筆
            var response = await _client.From<SupabaseCustomer>()
                .Select("CustomerCode")
                .Order("CustomerCode", Ordering.Descending)
                .Limit(1)
                .Get();

            var maxCode = response.Models
                .Select(c => c.CustomerCode)
                .FirstOrDefault(code => !string.IsNullOrEmpty(code));

            return maxCode;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Lamp

    public async Task<List<Lamp>> GetAllLampsAsync()
    {
        if (!_isConfigured || _client == null)
            return new List<Lamp>();

        var response = await _client.From<SupabaseLamp>().Get();
        return response.Models.Select(l => l.ToLamp()).ToList();
    }

    public async Task<Lamp> UpsertLampAsync(Lamp lamp)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        var supabaseLamp = SupabaseLamp.FromLamp(lamp);
        await _client.From<SupabaseLamp>().Upsert(supabaseLamp);
        return lamp;
    }

    #endregion

    #region LampOrder

    public async Task<LampOrder?> GetLampOrderAsync(Guid id)
    {
        if (!_isConfigured || _client == null) return null;

        var idStr = id.ToString();
        var response = await _client
            .From<SupabaseLampOrder>()
            .Where(o => o.Id == idStr)
            .Single();

        return response?.ToLampOrder();
    }

    public async Task<List<LampOrder>> GetLampOrdersByCustomerAsync(Guid customerId)
    {
        if (!_isConfigured || _client == null)
            return new List<LampOrder>();

        var customerIdStr = customerId.ToString();
        var response = await _client
            .From<SupabaseLampOrder>()
            .Where(o => o.CustomerId == customerIdStr)
            .Get();

        return response.Models.Select(o => o.ToLampOrder()).ToList();
    }

    public async Task<LampOrder> UpsertLampOrderAsync(LampOrder order)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        var supabaseOrder = SupabaseLampOrder.FromLampOrder(order);
        await _client.From<SupabaseLampOrder>().Upsert(supabaseOrder);
        return order;
    }

    public async Task DeleteLampOrderAsync(Guid id)
    {
        if (!_isConfigured || _client == null) return;
        var idStr = id.ToString();
        await _client.From<SupabaseLampOrder>().Where(o => o.Id == idStr).Delete();
    }

    public async Task<bool> HasActiveOrderAsync(Guid customerId, int lampId)
    {
        if (!_isConfigured || _client == null)
            return false;

        try
        {
            var today = DateTime.Now.Date;
            var customerIdStr = customerId.ToString();

            var response = await _client
                .From<SupabaseLampOrder>()
                .Where(o => o.CustomerId == customerIdStr && o.LampId == lampId)
                .Get();

            return response.Models.Any(o => o.EndDate > today);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HasActiveOrderAsync 錯誤：{ex.Message}");
            return false;
        }
    }

    public async Task<int> GetCloudOrderCountAsync(int lampId, int year)
    {
        if (!_isConfigured || _client == null) return 0;

        try
        {
            var response = await _client
                .From<SupabaseLampOrder>()
                .Where(o => o.LampId == lampId && o.Year == year)
                .Get();

            return response.Models.Count;
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region ID 查詢（用於刪除同步）

    public async Task<HashSet<string>> GetAllCloudCustomerIdsAsync()
    {
        if (!_isConfigured || _client == null)
            return new HashSet<string>();

        try
        {
            var response = await _client.From<SupabaseCustomer>()
                .Select("Id")
                .Get();

            return response.Models.Select(c => c.Id).ToHashSet();
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    public async Task<HashSet<string>> GetAllCloudLampOrderIdsAsync()
    {
        if (!_isConfigured || _client == null)
            return new HashSet<string>();

        try
        {
            var response = await _client.From<SupabaseLampOrder>()
                .Select("Id")
                .Get();

            return response.Models.Select(o => o.Id).ToHashSet();
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    #endregion

    #region Sync

    public async Task<SyncResult> SyncToCloudAsync(DateTime? since = null)
    {
        var result = new SyncResult();

        if (!_isConfigured || _client == null)
        {
            result.ErrorMessage = "Supabase 未設定";
            return result;
        }

        try
        {
            // 增量上傳：只上傳 since 之後修改的資料
            IQueryable<Customer> customerQuery = _localContext.Customers.AsNoTracking();
            IQueryable<LampOrder> orderQuery = _localContext.LampOrders.AsNoTracking();

            if (since.HasValue)
            {
                customerQuery = customerQuery.Where(c => c.UpdatedAt > since.Value);
                orderQuery = orderQuery.Where(o => o.UpdatedAt > since.Value);
            }

            var customers = await customerQuery.ToListAsync();
            foreach (var customer in customers)
            {
                await UpsertCustomerAsync(customer);
                result.CustomersUploaded++;
            }

            // 燈種很少，全量同步即可
            var lamps = await _localContext.Lamps.AsNoTracking().ToListAsync();
            foreach (var lamp in lamps)
            {
                await UpsertLampAsync(lamp);
            }

            var orders = await orderQuery.ToListAsync();
            foreach (var order in orders)
            {
                await UpsertLampOrderAsync(order);
                result.OrdersUploaded++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<SyncResult> SyncFromCloudAsync(DateTime? since = null)
    {
        var result = new SyncResult();

        if (!_isConfigured || _client == null)
        {
            result.ErrorMessage = "Supabase 未設定";
            return result;
        }

        try
        {
            // 先取得 SyncQueue 中待刪除的 ID，避免把已刪除的資料又從雲端拉回來
            var pendingDeletes = await _localContext.SyncQueue
                .Where(q => q.Operation == SyncOperation.Delete)
                .ToListAsync();
            var pendingDeleteCustomerIds = pendingDeletes
                .Where(q => q.EntityType == SyncEntityType.Customer)
                .Select(q => q.EntityId)
                .ToHashSet();
            var pendingDeleteOrderIds = pendingDeletes
                .Where(q => q.EntityType == SyncEntityType.LampOrder)
                .Select(q => q.EntityId)
                .ToHashSet();

            // 燈種全量同步（資料量極少）
            var cloudLamps = await GetAllLampsAsync();
            foreach (var lamp in cloudLamps)
            {
                var existing = await _localContext.Lamps.FindAsync(lamp.Id);
                if (existing == null)
                {
                    _localContext.Lamps.Add(lamp);
                }
                else
                {
                    existing.LampCode = lamp.LampCode;
                    existing.LampName = lamp.LampName;
                }
            }

            // 增量下載客戶：只取 since 之後更新的
            List<SupabaseCustomer> cloudCustomerModels;
            if (since.HasValue)
            {
                var sinceStr = since.Value.ToUniversalTime().ToString("o");
                var response = await _client!.From<SupabaseCustomer>()
                    .Filter("UpdatedAt", Operator.GreaterThan, sinceStr)
                    .Get();
                cloudCustomerModels = response.Models;
            }
            else
            {
                var response = await _client!.From<SupabaseCustomer>().Get();
                cloudCustomerModels = response.Models;
            }

            foreach (var supabaseCustomer in cloudCustomerModels)
            {
                if (pendingDeleteCustomerIds.Contains(supabaseCustomer.Id))
                    continue;

                var customer = supabaseCustomer.ToCustomer();
                var existing = await _localContext.Customers.FindAsync(customer.Id);
                if (existing == null)
                {
                    _localContext.Customers.Add(customer);
                    result.CustomersDownloaded++;
                }
                else if (customer.UpdatedAt > existing.UpdatedAt)
                {
                    existing.Name = customer.Name;
                    existing.Phone = customer.Phone;
                    existing.Mobile = customer.Mobile;
                    existing.Address = customer.Address;
                    existing.Note = customer.Note;
                    existing.Village = customer.Village;
                    existing.PostalCode = customer.PostalCode;
                    existing.BirthYear = customer.BirthYear;
                    existing.BirthMonth = customer.BirthMonth;
                    existing.BirthDay = customer.BirthDay;
                    existing.BirthHour = customer.BirthHour;
                    if (string.IsNullOrEmpty(existing.CustomerCode))
                        existing.CustomerCode = customer.CustomerCode;
                    existing.UpdatedAt = customer.UpdatedAt;
                    result.CustomersDownloaded++;
                }
            }

            // 增量下載訂單：只取 since 之後更新的
            List<SupabaseLampOrder> cloudOrderModels;
            if (since.HasValue)
            {
                var sinceStr = since.Value.ToUniversalTime().ToString("o");
                var response = await _client!.From<SupabaseLampOrder>()
                    .Filter("UpdatedAt", Operator.GreaterThan, sinceStr)
                    .Get();
                cloudOrderModels = response.Models;
            }
            else
            {
                var response = await _client!.From<SupabaseLampOrder>().Get();
                cloudOrderModels = response.Models;
            }

            foreach (var supabaseOrder in cloudOrderModels)
            {
                if (pendingDeleteOrderIds.Contains(supabaseOrder.Id))
                    continue;

                var order = supabaseOrder.ToLampOrder();
                var existing = await _localContext.LampOrders.FindAsync(order.Id);

                if (existing == null)
                {
                    _localContext.LampOrders.Add(order);
                    result.OrdersDownloaded++;
                }
                else if (order.UpdatedAt > existing.UpdatedAt)
                {
                    existing.CustomerId = order.CustomerId;
                    existing.LampId = order.LampId;
                    existing.StartDate = order.StartDate;
                    existing.EndDate = order.EndDate;
                    existing.Year = order.Year;
                    existing.Price = order.Price;
                    existing.Note = order.Note;
                    existing.UpdatedAt = order.UpdatedAt;
                    result.OrdersDownloaded++;
                }
            }

            await _localContext.SaveChangesAsync();
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion
}

#region Supabase Models

[Table("Customers")]
public class SupabaseCustomer : BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("Id", false)]
    [Column("Id")]
    public string Id { get; set; } = string.Empty;

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Phone")]
    public string? Phone { get; set; }

    [Column("Mobile")]
    public string? Mobile { get; set; }

    [Column("Address")]
    public string? Address { get; set; }

    [Column("Note")]
    public string? Note { get; set; }

    [Column("Village")]
    public string? Village { get; set; }

    [Column("PostalCode")]
    public string? PostalCode { get; set; }

    [Column("BirthYear")]
    public int? BirthYear { get; set; }

    [Column("BirthMonth")]
    public int? BirthMonth { get; set; }

    [Column("BirthDay")]
    public int? BirthDay { get; set; }

    [Column("BirthHour")]
    public string? BirthHour { get; set; }

    [Column("CustomerCode")]
    public string? CustomerCode { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    public Customer ToCustomer() => new()
    {
        Id = Guid.Parse(Id),
        Name = Name,
        Phone = Phone,
        Mobile = Mobile,
        Address = Address,
        Note = Note,
        Village = Village,
        PostalCode = PostalCode,
        BirthYear = BirthYear,
        BirthMonth = BirthMonth,
        BirthDay = BirthDay,
        BirthHour = BirthHour,
        CustomerCode = CustomerCode,
        UpdatedAt = UpdatedAt
    };

    public static SupabaseCustomer FromCustomer(Customer c) => new()
    {
        Id = c.Id.ToString(),
        Name = c.Name,
        Phone = c.Phone,
        Mobile = c.Mobile,
        Address = c.Address,
        Note = c.Note,
        Village = c.Village,
        PostalCode = c.PostalCode,
        BirthYear = c.BirthYear,
        BirthMonth = c.BirthMonth,
        BirthDay = c.BirthDay,
        BirthHour = c.BirthHour,
        CustomerCode = c.CustomerCode,
        UpdatedAt = c.UpdatedAt
    };
}

[Table("Lamps")]
public class SupabaseLamp : BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("Id")]
    [Column("Id")]
    public int Id { get; set; }

    [Column("LampCode")]
    public string LampCode { get; set; } = string.Empty;

    [Column("LampName")]
    public string LampName { get; set; } = string.Empty;

    [Column("Temple")]
    public string? Temple { get; set; }

    [Column("Deity")]
    public string? Deity { get; set; }

    [Column("MaxQuota")]
    public int MaxQuota { get; set; }

    public Lamp ToLamp() => new()
    {
        Id = Id,
        LampCode = LampCode,
        LampName = LampName,
        Temple = Temple,
        Deity = Deity,
        MaxQuota = MaxQuota
    };

    public static SupabaseLamp FromLamp(Lamp l) => new()
    {
        Id = l.Id,
        LampCode = l.LampCode,
        LampName = l.LampName,
        Temple = l.Temple,
        Deity = l.Deity,
        MaxQuota = l.MaxQuota
    };
}

[Table("LampOrders")]
public class SupabaseLampOrder : BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("Id", false)]
    [Column("Id")]
    public string Id { get; set; } = string.Empty;

    [Column("CustomerId")]
    public string CustomerId { get; set; } = string.Empty;

    [Column("LampId")]
    public int LampId { get; set; }

    [Column("StartDate")]
    public DateTime StartDate { get; set; }

    [Column("EndDate")]
    public DateTime EndDate { get; set; }

    [Column("Year")]
    public int Year { get; set; }

    [Column("Price")]
    public decimal Price { get; set; }

    [Column("Note")]
    public string? Note { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    public LampOrder ToLampOrder() => new()
    {
        Id = Guid.Parse(Id),
        CustomerId = Guid.Parse(CustomerId),
        LampId = LampId,
        StartDate = StartDate,
        EndDate = EndDate,
        Year = Year,
        Price = Price,
        Note = Note,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static SupabaseLampOrder FromLampOrder(LampOrder o) => new()
    {
        Id = o.Id.ToString(),
        CustomerId = o.CustomerId.ToString(),
        LampId = o.LampId,
        StartDate = o.StartDate,
        EndDate = o.EndDate,
        Year = o.Year,
        Price = o.Price,
        Note = o.Note,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };
}

#endregion
