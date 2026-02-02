using System.Text;
using Microsoft.Data.SqlClient;
using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Exceptions;
using TempleLamp.Api.Repositories;

namespace TempleLamp.Api.Services;

/// <summary>
/// 訂單服務介面
/// </summary>
public interface IOrderService
{
    Task<OrderResponse> GetByIdAsync(int orderId);
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, string workstationId);
    Task<OrderResponse> ConfirmAsync(int orderId, ConfirmOrderRequest request, string workstationId);
    Task<OrderResponse> CancelAsync(int orderId, CancelOrderRequest request, string workstationId);
    Task<ReceiptResponse> GetReceiptAsync(int orderId);
    Task<PrintResultResponse> PrintReceiptAsync(int orderId, PrintReceiptRequest request, string workstationId);
}

/// <summary>
/// 訂單服務實作
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILampSlotRepository _lampSlotRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ILampSlotRepository lampSlotRepository,
        ICustomerRepository customerRepository,
        IAuditRepository auditRepository,
        IDbConnectionFactory connectionFactory,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _lampSlotRepository = lampSlotRepository;
        _customerRepository = customerRepository;
        _auditRepository = auditRepository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<OrderResponse> GetByIdAsync(int orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order == null)
        {
            throw new NotFoundException(ErrorCodes.ORDER_NOT_FOUND, $"找不到訂單 ID: {orderId}");
        }

        return order;
    }

    /// <summary>
    /// 建立訂單（交易性操作）
    /// 1. 驗證客戶存在
    /// 2. 驗證所有燈位已被此工作站鎖定
    /// 3. 產生訂單編號
    /// 4. 建立訂單與明細
    /// 5. 記錄稽核
    /// </summary>
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, string workstationId)
    {
        // 驗證客戶
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
        if (customer == null)
        {
            throw new NotFoundException(ErrorCodes.CUSTOMER_NOT_FOUND, $"找不到客戶 ID: {request.CustomerId}");
        }

        // 驗證燈位
        var slots = new List<LampSlotResponse>();
        foreach (var slotId in request.LampSlotIds)
        {
            var slot = await _lampSlotRepository.GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
            }

            // 確認燈位已被此工作站鎖定
            if (slot.Status != "LOCKED" || slot.LockedByWorkstation != workstationId)
            {
                throw new BusinessException(ErrorCodes.SLOT_NOT_LOCKED_BY_YOU,
                    $"燈位 {slot.SlotNumber} 未被您的工作站鎖定");
            }

            // 確認鎖定未過期
            if (slot.LockExpiresAt.HasValue && slot.LockExpiresAt.Value < DateTime.Now)
            {
                throw new BusinessException(ErrorCodes.SLOT_LOCK_EXPIRED,
                    $"燈位 {slot.SlotNumber} 的鎖定已過期");
            }

            slots.Add(slot);
        }

        // 計算總金額
        var totalAmount = slots.Sum(s => s.Price);

        // 開始交易
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 產生訂單編號
            var orderNumber = await _orderRepository.GenerateOrderNumberAsync(transaction);

            // 建立訂單
            var orderData = new OrderCreateData
            {
                CustomerId = request.CustomerId,
                OrderNumber = orderNumber,
                LightingName = request.LightingName,
                BlessingContent = request.BlessingContent,
                TotalAmount = totalAmount,
                CreatedByWorkstation = workstationId,
                Notes = request.Notes
            };

            var orderId = await _orderRepository.CreateAsync(orderData, transaction);

            // 建立訂單明細
            foreach (var slot in slots)
            {
                await _orderRepository.CreateOrderItemAsync(orderId, slot.SlotId, slot.Price, transaction);
            }

            // 記錄稽核
            await _auditRepository.LogAsync("CREATE", "Order", orderId, workstationId,
                $"訂單編號: {orderNumber}, 燈位數: {slots.Count}, 總金額: {totalAmount}", transaction);

            await transaction.CommitAsync();

            _logger.LogInformation("訂單建立成功: OrderId={OrderId}, OrderNumber={OrderNumber}, TotalAmount={TotalAmount}",
                orderId, orderNumber, totalAmount);

            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "訂單建立失敗: CustomerId={CustomerId}, SlotCount={SlotCount}",
                request.CustomerId, request.LampSlotIds.Count);
            throw new BusinessException(ErrorCodes.ORDER_CREATE_FAILED, "訂單建立失敗: " + ex.Message);
        }
    }

    /// <summary>
    /// 確認訂單（付款完成）
    /// 1. 驗證訂單狀態
    /// 2. 驗證付款金額
    /// 3. 建立付款記錄
    /// 4. 更新訂單狀態
    /// 5. 將燈位標記為已售出
    /// </summary>
    public async Task<OrderResponse> ConfirmAsync(int orderId, ConfirmOrderRequest request, string workstationId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new NotFoundException(ErrorCodes.ORDER_NOT_FOUND, $"找不到訂單 ID: {orderId}");
        }

        if (order.Status == "CONFIRMED")
        {
            throw new BusinessException(ErrorCodes.ORDER_ALREADY_CONFIRMED, "訂單已確認付款");
        }

        if (order.Status == "CANCELLED")
        {
            throw new BusinessException(ErrorCodes.ORDER_ALREADY_CANCELLED, "訂單已取消");
        }

        if (order.Status != "PENDING")
        {
            throw new BusinessException(ErrorCodes.ORDER_INVALID_STATUS, $"訂單狀態不正確: {order.Status}");
        }

        // 驗證付款金額（實收不可少於應付）
        if (request.AmountReceived < order.TotalAmount)
        {
            throw new BusinessException(ErrorCodes.PAYMENT_AMOUNT_MISMATCH,
                $"實收金額 {request.AmountReceived} 不足，應付 {order.TotalAmount}");
        }

        // 開始交易
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 建立付款記錄
            var paymentData = new PaymentCreateData
            {
                OrderId = orderId,
                PaymentMethod = request.PaymentMethod,
                AmountDue = order.TotalAmount,
                AmountReceived = request.AmountReceived,
                ReceivedByWorkstation = workstationId,
                Notes = request.PaymentNotes
            };

            await _orderRepository.CreatePaymentAsync(paymentData, transaction);

            // 更新訂單狀態
            await _orderRepository.UpdateStatusAsync(orderId, "CONFIRMED", transaction);

            // 將所有燈位標記為已售出
            foreach (var item in order.Items)
            {
                await _lampSlotRepository.MarkAsSoldAsync(item.SlotId, transaction);
            }

            // 記錄稽核
            await _auditRepository.LogAsync("CONFIRM", "Order", orderId, workstationId,
                $"付款方式: {request.PaymentMethod}, 實收: {request.AmountReceived}, 找零: {request.AmountReceived - order.TotalAmount}",
                transaction);

            await transaction.CommitAsync();

            _logger.LogInformation("訂單確認成功: OrderId={OrderId}, PaymentMethod={PaymentMethod}, Amount={Amount}",
                orderId, request.PaymentMethod, request.AmountReceived);

            return await GetByIdAsync(orderId);
        }
        catch (BusinessException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "訂單確認失敗: OrderId={OrderId}", orderId);
            throw new BusinessException(ErrorCodes.ORDER_PAYMENT_FAILED, "付款處理失敗: " + ex.Message);
        }
    }

    /// <summary>
    /// 取消訂單
    /// 1. 驗證訂單狀態
    /// 2. 更新訂單狀態
    /// 3. 釋放燈位（若尚未售出）
    /// </summary>
    public async Task<OrderResponse> CancelAsync(int orderId, CancelOrderRequest request, string workstationId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new NotFoundException(ErrorCodes.ORDER_NOT_FOUND, $"找不到訂單 ID: {orderId}");
        }

        if (order.Status == "CONFIRMED")
        {
            throw new BusinessException(ErrorCodes.ORDER_ALREADY_CONFIRMED, "已確認的訂單無法取消");
        }

        if (order.Status == "CANCELLED")
        {
            throw new BusinessException(ErrorCodes.ORDER_ALREADY_CANCELLED, "訂單已取消");
        }

        // 開始交易
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 更新訂單狀態
            await _orderRepository.UpdateStatusAsync(orderId, "CANCELLED", transaction);

            // 記錄稽核
            await _auditRepository.LogAsync("CANCEL", "Order", orderId, workstationId,
                $"取消原因: {request.Reason}", transaction);

            await transaction.CommitAsync();

            // 釋放相關燈位（非交易內，失敗不影響取消結果）
            foreach (var item in order.Items)
            {
                try
                {
                    await _lampSlotRepository.ReleaseAsync(item.SlotId, order.CreatedByWorkstation);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "釋放燈位失敗（非嚴重）: SlotId={SlotId}", item.SlotId);
                }
            }

            _logger.LogInformation("訂單取消成功: OrderId={OrderId}, Reason={Reason}", orderId, request.Reason);

            return await GetByIdAsync(orderId);
        }
        catch (BusinessException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "訂單取消失敗: OrderId={OrderId}", orderId);
            throw;
        }
    }

    public async Task<ReceiptResponse> GetReceiptAsync(int orderId)
    {
        var order = await GetByIdAsync(orderId);

        if (order.Status != "CONFIRMED")
        {
            throw new BusinessException(ErrorCodes.ORDER_INVALID_STATUS, "僅已確認的訂單可產生收據");
        }

        var receipt = new ReceiptResponse
        {
            ReceiptNumber = $"R-{order.OrderNumber}",
            Order = order,
            TempleName = "範例宮",  // TODO: 從設定檔讀取
            TempleAddress = "台北市中正區範例路一號",
            TemplePhone = "02-1234-5678",
            PrintTime = DateTime.Now,
            FormattedContent = FormatReceiptContent(order)
        };

        return receipt;
    }

    public async Task<PrintResultResponse> PrintReceiptAsync(int orderId, PrintReceiptRequest request, string workstationId)
    {
        var receipt = await GetReceiptAsync(orderId);

        // 記錄列印稽核
        await _auditRepository.LogAsync("PRINT", "Receipt", orderId, workstationId,
            $"收據編號: {receipt.ReceiptNumber}, 份數: {request.Copies}");

        // 實際列印邏輯（由 WPF 端處理，這裡僅回傳收據資料）
        return new PrintResultResponse
        {
            Success = true,
            ReceiptNumber = receipt.ReceiptNumber,
            PrinterName = request.PrinterName ?? "DEFAULT",
            PrintTime = DateTime.Now
        };
    }

    private string FormatReceiptContent(OrderResponse order)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine("               點 燈 收 據              ");
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"訂單編號: {order.OrderNumber}");
        sb.AppendLine($"日    期: {order.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────");
        sb.AppendLine($"客戶姓名: {order.CustomerName}");
        sb.AppendLine($"點燈者名: {order.LightingName}");

        if (!string.IsNullOrEmpty(order.BlessingContent))
        {
            sb.AppendLine($"祈福內容: {order.BlessingContent}");
        }

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────");
        sb.AppendLine("燈位明細:");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"  {item.LampTypeName} - {item.SlotNumber}");
            sb.AppendLine($"    {item.Zone} / {item.Year}年    NT${item.UnitPrice:N0}");
        }

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────");
        sb.AppendLine($"合    計: NT${order.TotalAmount:N0}");

        if (order.Payment != null)
        {
            sb.AppendLine($"付款方式: {order.Payment.PaymentMethod}");
            sb.AppendLine($"實    收: NT${order.Payment.AmountReceived:N0}");
            if (order.Payment.ChangeAmount > 0)
            {
                sb.AppendLine($"找    零: NT${order.Payment.ChangeAmount:N0}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine("          感謝您的護持 功德無量          ");
        sb.AppendLine("═══════════════════════════════════════");

        return sb.ToString();
    }
}
