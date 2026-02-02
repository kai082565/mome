using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Exceptions;
using TempleLamp.Api.Repositories;

namespace TempleLamp.Api.Services;

/// <summary>
/// 客戶服務介面
/// </summary>
public interface ICustomerService
{
    Task<CustomerSearchResponse> SearchByPhoneAsync(string phone);
    Task<CustomerResponse> GetByIdAsync(int customerId);
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request);
}

/// <summary>
/// 客戶服務實作
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ICustomerRepository customerRepository,
        IAuditRepository auditRepository,
        ILogger<CustomerService> logger)
    {
        _customerRepository = customerRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<CustomerSearchResponse> SearchByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 3)
        {
            throw new BusinessException(ErrorCodes.CUSTOMER_PHONE_INVALID, "電話號碼至少需輸入 3 個字元");
        }

        var customers = await _customerRepository.SearchByPhoneAsync(phone);
        var customerList = customers.ToList();

        _logger.LogDebug("客戶搜尋: Phone={Phone}, 結果數={Count}", phone, customerList.Count);

        return new CustomerSearchResponse
        {
            Customers = customerList,
            TotalCount = customerList.Count
        };
    }

    public async Task<CustomerResponse> GetByIdAsync(int customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);

        if (customer == null)
        {
            throw new NotFoundException(ErrorCodes.CUSTOMER_NOT_FOUND, $"找不到客戶 ID: {customerId}");
        }

        return customer;
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request)
    {
        // 檢查電話是否已存在
        var existing = await _customerRepository.GetByPhoneAsync(request.Phone);
        if (existing != null)
        {
            _logger.LogWarning("電話號碼已存在: Phone={Phone}, ExistingCustomerId={CustomerId}", request.Phone, existing.CustomerId);
            // 返回既有客戶
            return existing;
        }

        var customerId = await _customerRepository.CreateAsync(
            request.Name,
            request.Phone,
            request.Address,
            request.Notes
        );

        _logger.LogInformation("新客戶建立成功: CustomerId={CustomerId}, Phone={Phone}", customerId, request.Phone);

        return await GetByIdAsync(customerId);
    }
}
