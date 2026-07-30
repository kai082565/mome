using TempleLampSystem.Models;

namespace TempleLampSystem.Services.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetWithOrdersAsync(Guid customerId);
    Task<List<Customer>> SearchByPhoneWithOrdersAsync(string phone);

    /// <summary>
    /// 查詢與指定客戶同電話或同手機的家人（不含自己）
    /// </summary>
    Task<List<Customer>> GetFamilyMembersAsync(Guid customerId);

    /// <summary>
    /// 根據電話或手機號碼查詢所有相關客戶（用於新增客戶時檢測）
    /// </summary>
    Task<List<Customer>> FindByPhoneOrMobileAsync(string? phone, string? mobile);

    /// <summary>
    /// 取得下一個客戶流水編號（6碼，如 000001）
    /// </summary>
    Task<string> GetNextCustomerCodeAsync();

    /// <summary>
    /// 比對姓名+電話 或 姓名+地址 是否已有完全相同的客戶（用於新增客戶時擋重複，排除指定 Id 本身）
    /// </summary>
    Task<Customer?> FindDuplicateAsync(string name, string? phone, string? address, Guid? excludeId = null);
}
