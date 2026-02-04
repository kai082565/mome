using Microsoft.EntityFrameworkCore;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using TempleLampSystem.Models;

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

        var response = await _client
            .From<SupabaseCustomer>()
            .Where(c => c.Id == id.ToString())
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
        await _client.From<SupabaseCustomer>().Where(c => c.Id == id.ToString()).Delete();
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

        var response = await _client
            .From<SupabaseLampOrder>()
            .Where(o => o.Id == id.ToString())
            .Single();

        return response?.ToLampOrder();
    }

    public async Task<List<LampOrder>> GetLampOrdersByCustomerAsync(Guid customerId)
    {
        if (!_isConfigured || _client == null)
            return new List<LampOrder>();

        var response = await _client
            .From<SupabaseLampOrder>()
            .Where(o => o.CustomerId == customerId.ToString())
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
        await _client.From<SupabaseLampOrder>().Where(o => o.Id == id.ToString()).Delete();
    }

    #endregion

    #region Sync

    public async Task<SyncResult> SyncToCloudAsync()
    {
        var result = new SyncResult();

        if (!_isConfigured || _client == null)
        {
            result.ErrorMessage = "Supabase 未設定";
            return result;
        }

        try
        {
            var customers = await _localContext.Customers.ToListAsync();
            foreach (var customer in customers)
            {
                await UpsertCustomerAsync(customer);
                result.CustomersUploaded++;
            }

            var lamps = await _localContext.Lamps.ToListAsync();
            foreach (var lamp in lamps)
            {
                await UpsertLampAsync(lamp);
            }

            var orders = await _localContext.LampOrders.ToListAsync();
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

    public async Task<SyncResult> SyncFromCloudAsync()
    {
        var result = new SyncResult();

        if (!_isConfigured || _client == null)
        {
            result.ErrorMessage = "Supabase 未設定";
            return result;
        }

        try
        {
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

            var cloudCustomers = await GetAllCustomersAsync();
            foreach (var customer in cloudCustomers)
            {
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
                    existing.UpdatedAt = customer.UpdatedAt;
                    result.CustomersDownloaded++;
                }
            }

            var response = await _client!.From<SupabaseLampOrder>().Get();
            foreach (var supabaseOrder in response.Models)
            {
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

    public Lamp ToLamp() => new()
    {
        Id = Id,
        LampCode = LampCode,
        LampName = LampName
    };

    public static SupabaseLamp FromLamp(Lamp l) => new()
    {
        Id = l.Id,
        LampCode = l.LampCode,
        LampName = l.LampName
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
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };
}

#endregion
