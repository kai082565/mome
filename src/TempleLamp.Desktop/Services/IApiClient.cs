using TempleLamp.Desktop.Models;

namespace TempleLamp.Desktop.Services;

/// <summary>
/// API 客戶端介面
/// </summary>
public interface IApiClient
{
    // ===== 燈位相關 =====
    Task<ApiResponse<List<LampType>>> GetLampTypesAsync();
    Task<ApiResponse<List<LampSlot>>> GetLampSlotsAsync(int? lampTypeId = null, string? zone = null, bool availableOnly = false, int? year = null);
    Task<ApiResponse<LampSlot>> GetLampSlotByIdAsync(int slotId);
    Task<ApiResponse<LockLampSlotResponse>> LockLampSlotAsync(int slotId, LockLampSlotRequest request);
    Task<ApiResponse<bool>> ReleaseLampSlotAsync(int slotId);

    // ===== 客戶相關 =====
    Task<ApiResponse<List<Customer>>> SearchCustomersAsync(string phone);
    Task<ApiResponse<Customer>> GetCustomerByIdAsync(int customerId);
    Task<ApiResponse<Customer>> CreateCustomerAsync(CreateCustomerRequest request);

    // ===== 訂單相關 =====
    Task<ApiResponse<Order>> CreateOrderAsync(CreateOrderRequest request);
    Task<ApiResponse<Order>> GetOrderByIdAsync(int orderId);
    Task<ApiResponse<Order>> ConfirmOrderAsync(int orderId, ConfirmOrderRequest request);
    Task<ApiResponse<Order>> CancelOrderAsync(int orderId, string reason);

    // ===== 收據相關 =====
    Task<ApiResponse<Receipt>> GetReceiptAsync(int orderId);
    Task<ApiResponse<PrintResult>> PrintReceiptAsync(int orderId, PrintReceiptRequest request);
}
