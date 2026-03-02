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

    #region Staff

    public async Task UpsertStaffAsync(Staff staff)
    {
        if (!_isConfigured || _client == null) return;
        var model = SupabaseStaff.FromStaff(staff);
        await _client.From<SupabaseStaff>().Upsert(model);
    }

    public async Task UpsertStaffBatchAsync(List<Staff> staffList)
    {
        if (!_isConfigured || _client == null || staffList.Count == 0) return;
        var models = staffList.Select(SupabaseStaff.FromStaff).ToList();
        await _client.From<SupabaseStaff>().Upsert(models);
    }

    public async Task<List<Staff>> GetAllStaffAsync()
    {
        if (!_isConfigured || _client == null) return new List<Staff>();
        try
        {
            var response = await _client.From<SupabaseStaff>().Get();
            return response.Models.Select(s => s.ToStaff()).ToList();
        }
        catch
        {
            return new List<Staff>();
        }
    }

    #endregion

    #region Customer

    public async Task<Customer> UpsertCustomerAsync(Customer customer)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        var supabaseCustomer = SupabaseCustomer.FromCustomer(customer);
        await _client.From<SupabaseCustomer>().Upsert(supabaseCustomer);
        return customer;
    }

    public async Task UpsertCustomerBatchAsync(List<Customer> customers)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        if (customers.Count == 0) return;

        var supabaseModels = customers.Select(SupabaseCustomer.FromCustomer).ToList();
        await _client.From<SupabaseCustomer>().Upsert(supabaseModels);
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

    public async Task UpsertLampOrderBatchAsync(List<LampOrder> orders)
    {
        if (!_isConfigured || _client == null)
            throw new InvalidOperationException("Supabase 未設定");

        if (orders.Count == 0) return;

        var supabaseModels = orders.Select(SupabaseLampOrder.FromLampOrder).ToList();
        await _client.From<SupabaseLampOrder>().Upsert(supabaseModels);
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

            // Staff 全量同步（數量少）
            var staffList = await _localContext.Staff.AsNoTracking().ToListAsync();
            if (staffList.Count > 0)
                await UpsertStaffBatchAsync(staffList);

            // 燈種很少，全量同步即可
            var lamps = await _localContext.Lamps.AsNoTracking().ToListAsync();
            foreach (var lamp in lamps)
            {
                await UpsertLampAsync(lamp);
            }

            // 批量上傳客戶（每批 200 筆）
            var customers = await customerQuery.ToListAsync();
            for (var i = 0; i < customers.Count; i += 200)
            {
                var batch = customers.Skip(i).Take(200).ToList();
                await UpsertCustomerBatchAsync(batch);
                result.CustomersUploaded += batch.Count;
            }

            // 批量上傳訂單（每批 200 筆）
            var orders = await orderQuery.ToListAsync();
            for (var i = 0; i < orders.Count; i += 200)
            {
                var batch = orders.Skip(i).Take(200).ToList();
                await UpsertLampOrderBatchAsync(batch);
                result.OrdersUploaded += batch.Count;
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
            // 從雲端同步 Staff（雙向，以 Id 為主鍵，雲端有就覆蓋本地）
            var cloudStaff = await GetAllStaffAsync();
            if (cloudStaff.Count > 0)
            {
                var localStaffIds = await _localContext.Staff.Select(s => s.Id).ToListAsync();
                var localStaffIdSet = localStaffIds.ToHashSet();
                foreach (var staff in cloudStaff)
                {
                    if (!localStaffIdSet.Contains(staff.Id))
                    {
                        _localContext.Staff.Add(staff);
                    }
                    else
                    {
                        var existing = await _localContext.Staff.FindAsync(staff.Id);
                        if (existing != null)
                        {
                            existing.Name = staff.Name;
                            existing.PasswordHash = staff.PasswordHash;
                            existing.Salt = staff.Salt;
                            existing.Role = staff.Role;
                            existing.IsActive = staff.IsActive;
                        }
                    }
                }
            }

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
            var localLampIds = await _localContext.Lamps.Select(l => l.Id).ToListAsync();
            var localLampIdSet = localLampIds.ToHashSet();
            foreach (var lamp in cloudLamps)
            {
                if (!localLampIdSet.Contains(lamp.Id))
                {
                    _localContext.Lamps.Add(lamp);
                }
                else
                {
                    var existing = await _localContext.Lamps.FindAsync(lamp.Id);
                    if (existing != null)
                    {
                        existing.LampCode = lamp.LampCode;
                        existing.LampName = lamp.LampName;
                    }
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

            // 批量讀取本地客戶到字典，避免在循環中逐筆查詢（N+1 問題）
            var cloudCustomerIds = cloudCustomerModels
                .Select(c => c.Id)
                .Where(id => !pendingDeleteCustomerIds.Contains(id))
                .Select(id => Guid.Parse(id))
                .ToList();
            var localCustomerDict = await _localContext.Customers
                .Where(c => cloudCustomerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            foreach (var supabaseCustomer in cloudCustomerModels)
            {
                if (pendingDeleteCustomerIds.Contains(supabaseCustomer.Id))
                    continue;

                var customer = supabaseCustomer.ToCustomer();
                if (!localCustomerDict.TryGetValue(customer.Id, out var existing))
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

            // 批量讀取本地訂單到字典，避免在循環中逐筆查詢（N+1 問題）
            var cloudOrderIds = cloudOrderModels
                .Select(o => o.Id)
                .Where(id => !pendingDeleteOrderIds.Contains(id))
                .Select(id => Guid.Parse(id))
                .ToList();
            var localOrderDict = await _localContext.LampOrders
                .Where(o => cloudOrderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id);

            foreach (var supabaseOrder in cloudOrderModels)
            {
                if (pendingDeleteOrderIds.Contains(supabaseOrder.Id))
                    continue;

                var order = supabaseOrder.ToLampOrder();
                if (!localOrderDict.TryGetValue(order.Id, out var existing))
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
                    existing.StaffId = order.StaffId;
                    existing.StaffName = order.StaffName;
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

[Table("Staff")]
public class SupabaseStaff : BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("Id", false)]
    [Column("Id")]
    public string Id { get; set; } = string.Empty;

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("Salt")]
    public string Salt { get; set; } = string.Empty;

    [Column("Role")]
    public int Role { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    public Staff ToStaff() => new()
    {
        Id = Id,
        Name = Name,
        PasswordHash = PasswordHash,
        Salt = Salt,
        Role = (StaffRole)Role,
        IsActive = IsActive,
        CreatedAt = CreatedAt
    };

    public static SupabaseStaff FromStaff(Staff s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        PasswordHash = s.PasswordHash,
        Salt = s.Salt,
        Role = (int)s.Role,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt
    };
}

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

    [Column("StaffId")]
    public string? StaffId { get; set; }

    [Column("StaffName")]
    public string? StaffName { get; set; }

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
        UpdatedAt = UpdatedAt,
        StaffId = StaffId,
        StaffName = StaffName
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
        UpdatedAt = o.UpdatedAt,
        StaffId = o.StaffId,
        StaffName = o.StaffName
    };
}

#endregion
