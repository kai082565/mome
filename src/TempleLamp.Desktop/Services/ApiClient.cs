using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TempleLamp.Desktop.Models;

namespace TempleLamp.Desktop.Services;

/// <summary>
/// API 客戶端實作
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    #region 燈位相關

    public async Task<ApiResponse<List<LampType>>> GetLampTypesAsync()
    {
        return await GetAsync<List<LampType>>("api/lamp-slots/types");
    }

    public async Task<ApiResponse<List<LampSlot>>> GetLampSlotsAsync(int? lampTypeId = null, string? zone = null, bool availableOnly = false, int? year = null)
    {
        var queryParams = new List<string>();

        if (lampTypeId.HasValue)
            queryParams.Add($"lampTypeId={lampTypeId.Value}");

        if (!string.IsNullOrEmpty(zone))
            queryParams.Add($"zone={Uri.EscapeDataString(zone)}");

        if (availableOnly)
            queryParams.Add("availableOnly=true");

        if (year.HasValue)
            queryParams.Add($"year={year.Value}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync<List<LampSlot>>($"api/lamp-slots{query}");
    }

    public async Task<ApiResponse<LampSlot>> GetLampSlotByIdAsync(int slotId)
    {
        return await GetAsync<LampSlot>($"api/lamp-slots/{slotId}");
    }

    public async Task<ApiResponse<LockLampSlotResponse>> LockLampSlotAsync(int slotId, LockLampSlotRequest request)
    {
        return await PostAsync<LockLampSlotResponse>($"api/lamp-slots/{slotId}/lock", request);
    }

    public async Task<ApiResponse<bool>> ReleaseLampSlotAsync(int slotId)
    {
        return await PostAsync<bool>($"api/lamp-slots/{slotId}/release", null);
    }

    #endregion

    #region 客戶相關

    public async Task<ApiResponse<List<Customer>>> SearchCustomersAsync(string phone)
    {
        return await GetAsync<List<Customer>>($"api/customers/search?phone={Uri.EscapeDataString(phone)}");
    }

    public async Task<ApiResponse<Customer>> GetCustomerByIdAsync(int customerId)
    {
        return await GetAsync<Customer>($"api/customers/{customerId}");
    }

    public async Task<ApiResponse<Customer>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        return await PostAsync<Customer>("api/customers", request);
    }

    #endregion

    #region 訂單相關

    public async Task<ApiResponse<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        return await PostAsync<Order>("api/orders", request);
    }

    public async Task<ApiResponse<Order>> GetOrderByIdAsync(int orderId)
    {
        return await GetAsync<Order>($"api/orders/{orderId}");
    }

    public async Task<ApiResponse<Order>> ConfirmOrderAsync(int orderId, ConfirmOrderRequest request)
    {
        return await PostAsync<Order>($"api/orders/{orderId}/confirm", request);
    }

    public async Task<ApiResponse<Order>> CancelOrderAsync(int orderId, string reason)
    {
        return await PostAsync<Order>($"api/orders/{orderId}/cancel", new { Reason = reason });
    }

    #endregion

    #region 收據相關

    public async Task<ApiResponse<Receipt>> GetReceiptAsync(int orderId)
    {
        return await GetAsync<Receipt>($"api/orders/{orderId}/receipt");
    }

    public async Task<ApiResponse<PrintResult>> PrintReceiptAsync(int orderId, PrintReceiptRequest request)
    {
        return await PostAsync<PrintResult>($"api/orders/{orderId}/print", request);
    }

    #endregion

    #region Private Methods

    private async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            return await ParseResponseAsync<T>(response);
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = $"網路連線失敗: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCode = "UNKNOWN_ERROR",
                Message = $"未知錯誤: {ex.Message}"
            };
        }
    }

    private async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? request)
    {
        try
        {
            HttpResponseMessage response;

            if (request != null)
            {
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(endpoint, content);
            }
            else
            {
                response = await _httpClient.PostAsync(endpoint, null);
            }

            return await ParseResponseAsync<T>(response);
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = $"網路連線失敗: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCode = "UNKNOWN_ERROR",
                Message = $"未知錯誤: {ex.Message}"
            };
        }
    }

    private async Task<ApiResponse<T>> ParseResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);

            if (result != null)
            {
                return result;
            }

            return new ApiResponse<T>
            {
                Success = false,
                ErrorCode = "PARSE_ERROR",
                Message = "無法解析 API 回應"
            };
        }
        catch (JsonException)
        {
            // 嘗試解析為錯誤回應
            try
            {
                var errorResult = JsonSerializer.Deserialize<ApiResponse>(content, _jsonOptions);
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorCode = errorResult?.ErrorCode ?? "API_ERROR",
                    Message = errorResult?.Message ?? content
                };
            }
            catch
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorCode = "PARSE_ERROR",
                    Message = content
                };
            }
        }
    }

    #endregion
}
