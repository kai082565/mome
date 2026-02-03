using System.Data;
using System.Text;
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
    /// 建立訂單（單一 Transaction 內完成所有操作）
    ///
    /// 流程：
    /// 1. 驗證客戶存在（Transaction 外）
    /// 2. 驗證燈位存在並取得價格（Transaction 外）
    /// 3. 建立單一 Connection + Transaction
    /// 4. 呼叫 sp_TryLockLampSlot 鎖定所有燈位
    /// 5. 若任一燈位鎖定失敗 → Rollback → 回傳錯誤
    /// 6. Insert Orders
    /// 7. Insert OrderItems
    /// 8. Insert AuditLogs
    /// 9. Commit Transaction
    ///
    /// WorkstationId 由 Controller 從 HttpContext 取得後傳入
    /// </summary>
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, string workstationId)
    {
        // ===== Step 1: 驗證客戶存在（Transaction 外） =====
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
        if (customer == null)
        {
            throw new NotFoundException(ErrorCodes.CUSTOMER_NOT_FOUND, $"找不到客戶 ID: {request.CustomerId}");
        }

        // ===== Step 2: 驗證燈位存在（Transaction 外，僅驗證不取價格） =====
        var slotInfoList = new List<LampSlotResponse>();
        foreach (var slotId in request.LampSlotIds)
        {
            var slot = await _lampSlotRepository.GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException(ErrorCodes.SLOT_NOT_FOUND, $"找不到燈位 ID: {slotId}");
            }
            slotInfoList.Add(slot);
        }

        // ===== Step 3: 建立單一 Connection + Transaction =====
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // ===== Step 4: 呼叫 sp_TryLockLampSlot 鎖定所有燈位並取得價格 =====
            // 使用 Dictionary 儲存每個燈位的鎖定結果（包含 Transaction 內的價格）
            var lockResults = new Dictionary<int, LockResult>();

            foreach (var slot in slotInfoList)
            {
                var lockResult = await _lampSlotRepository.TryLockAsync(
                    slot.SlotId,
                    workstationId,
                    lockDurationSeconds: 600,  // 訂單處理鎖定 10 分鐘
                    connection,
                    transaction
                );

                // ===== Step 5: 若鎖定失敗 → Rollback → 回傳錯誤 =====
                if (!lockResult.Success)
                {
                    _logger.LogWarning(
                        "燈位鎖定失敗，訂單建立中止: SlotId={SlotId}, SlotNumber={SlotNumber}, Reason={Reason}, Workstation={Workstation}",
                        slot.SlotId, slot.SlotNumber, lockResult.FailureReason, workstationId);

                    // Rollback（會自動釋放已鎖定的燈位）
                    await RollbackTransactionAsync(transaction);

                    // 根據失敗原因回傳對應錯誤
                    if (lockResult.FailureReason?.Contains("已被鎖定") == true ||
                        lockResult.FailureReason?.Contains("LOCKED") == true)
                    {
                        throw new ConflictException(ErrorCodes.SLOT_LOCKED,
                            $"燈位 {slot.SlotNumber} 已被其他工作站鎖定");
                    }

                    if (lockResult.FailureReason?.Contains("已售出") == true ||
                        lockResult.FailureReason?.Contains("SOLD") == true)
                    {
                        throw new ConflictException(ErrorCodes.SLOT_NOT_AVAILABLE,
                            $"燈位 {slot.SlotNumber} 已售出");
                    }

                    throw new BusinessException(ErrorCodes.SLOT_LOCK_FAILED,
                        $"燈位 {slot.SlotNumber} 鎖定失敗: {lockResult.FailureReason}");
                }

                // 儲存鎖定結果（包含 Transaction 內取得的價格）
                lockResults[slot.SlotId] = lockResult;

                _logger.LogDebug("燈位鎖定成功: SlotId={SlotId}, Price={Price}, ExpiresAt={ExpiresAt}",
                    slot.SlotId, lockResult.Price, lockResult.LockExpiresAt);
            }

            // ===== Step 6: 計算總金額（使用 Transaction 內的價格） =====
            var totalAmount = lockResults.Values.Sum(r => r.Price);

            var orderNumber = await _orderRepository.GenerateOrderNumberAsync(connection, transaction);

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

            var orderId = await _orderRepository.CreateAsync(orderData, connection, transaction);

            // ===== Step 7: Insert OrderItems（使用 Transaction 內的價格） =====
            foreach (var slot in slotInfoList)
            {
                var price = lockResults[slot.SlotId].Price;
                await _orderRepository.CreateOrderItemAsync(orderId, slot.SlotId, price, connection, transaction);
            }

            // ===== Step 8: Insert AuditLogs =====
            var slotNumbers = string.Join(", ", slotInfoList.Select(s => s.SlotNumber));
            await _auditRepository.LogAsync(
                "CREATE",
                "Order",
                orderId,
                workstationId,
                $"訂單編號: {orderNumber}, 燈位: [{slotNumbers}], 總金額: {totalAmount:N0}",
                connection,
                transaction
            );

            // ===== Step 9: Commit Transaction =====
            transaction.Commit();

            _logger.LogInformation(
                "訂單建立成功: OrderId={OrderId}, OrderNumber={OrderNumber}, SlotCount={SlotCount}, TotalAmount={TotalAmount}, Workstation={Workstation}",
                orderId, orderNumber, slotInfoList.Count, totalAmount, workstationId);

            return await GetByIdAsync(orderId);
        }
        catch (BusinessException)
        {
            // 確保 Rollback（RollbackTransactionAsync 內部會忽略重複 Rollback）
            await RollbackTransactionAsync(transaction);
            throw;
        }
        catch (Exception ex)
        {
            // 確保 Rollback 後包裝成 BusinessException
            await RollbackTransactionAsync(transaction);

            _logger.LogError(ex,
                "訂單建立失敗(系統錯誤): CustomerId={CustomerId}, SlotCount={SlotCount}, Workstation={Workstation}",
                request.CustomerId, request.LampSlotIds.Count, workstationId);

            throw new BusinessException(ErrorCodes.ORDER_CREATE_FAILED, "訂單建立失敗: " + ex.Message);
        }
    }

    /// <summary>
    /// 確認訂單（付款完成）
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
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

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

            await _orderRepository.CreatePaymentAsync(paymentData, connection, transaction);

            // 更新訂單狀態
            await _orderRepository.UpdateStatusAsync(orderId, "CONFIRMED", connection, transaction);

            // 將所有燈位標記為已售出
            foreach (var item in order.Items)
            {
                await _lampSlotRepository.MarkAsSoldAsync(item.SlotId, connection, transaction);
            }

            // 記錄稽核
            await _auditRepository.LogAsync(
                "CONFIRM",
                "Order",
                orderId,
                workstationId,
                $"付款方式: {request.PaymentMethod}, 實收: {request.AmountReceived}, 找零: {request.AmountReceived - order.TotalAmount}",
                connection,
                transaction
            );

            transaction.Commit();

            _logger.LogInformation("訂單確認成功: OrderId={OrderId}, PaymentMethod={PaymentMethod}, Amount={Amount}",
                orderId, request.PaymentMethod, request.AmountReceived);

            return await GetByIdAsync(orderId);
        }
        catch (BusinessException)
        {
            await RollbackTransactionAsync(transaction);
            throw;
        }
        catch (Exception ex)
        {
            await RollbackTransactionAsync(transaction);
            _logger.LogError(ex, "訂單確認失敗: OrderId={OrderId}", orderId);
            throw new BusinessException(ErrorCodes.ORDER_PAYMENT_FAILED, "付款處理失敗: " + ex.Message);
        }
    }

    /// <summary>
    /// 取消訂單
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
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 更新訂單狀態
            await _orderRepository.UpdateStatusAsync(orderId, "CANCELLED", connection, transaction);

            // 釋放所有燈位
            foreach (var item in order.Items)
            {
                await _lampSlotRepository.ReleaseAsync(item.SlotId, order.CreatedByWorkstation, connection, transaction);
            }

            // 記錄稽核
            await _auditRepository.LogAsync(
                "CANCEL",
                "Order",
                orderId,
                workstationId,
                $"取消原因: {request.Reason}",
                connection,
                transaction
            );

            transaction.Commit();

            _logger.LogInformation("訂單取消成功: OrderId={OrderId}, Reason={Reason}", orderId, request.Reason);

            return await GetByIdAsync(orderId);
        }
        catch (BusinessException)
        {
            await RollbackTransactionAsync(transaction);
            throw;
        }
        catch (Exception ex)
        {
            await RollbackTransactionAsync(transaction);
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
            TempleName = "範例宮",
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

        return new PrintResultResponse
        {
            Success = true,
            ReceiptNumber = receipt.ReceiptNumber,
            PrinterName = request.PrinterName ?? "DEFAULT",
            PrintTime = DateTime.Now
        };
    }

    #region Private Methods

    /// <summary>
    /// 安全地 Rollback Transaction（忽略已完成或已 Rollback 的情況）
    /// </summary>
    private static async Task RollbackTransactionAsync(IDbTransaction transaction)
    {
        try
        {
            if (transaction.Connection != null)
            {
                transaction.Rollback();
            }
        }
        catch
        {
            // 忽略 Rollback 錯誤（可能已經 Rollback 或 Connection 已關閉）
        }

        await Task.CompletedTask;
    }

    private static string FormatReceiptContent(OrderResponse order)
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

    #endregion
}
