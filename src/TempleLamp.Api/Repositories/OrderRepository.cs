using Dapper;
using Microsoft.Data.SqlClient;
using TempleLamp.Api.DTOs.Responses;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 訂單資料存取介面
/// </summary>
public interface IOrderRepository
{
    Task<OrderResponse?> GetByIdAsync(int orderId);
    Task<OrderResponse?> GetByOrderNumberAsync(string orderNumber);
    Task<string> GenerateOrderNumberAsync(SqlTransaction transaction);
    Task<int> CreateAsync(OrderCreateData data, SqlTransaction transaction);
    Task<int> CreateOrderItemAsync(int orderId, int slotId, decimal unitPrice, SqlTransaction transaction);
    Task<bool> UpdateStatusAsync(int orderId, string status, SqlTransaction transaction);
    Task<int> CreatePaymentAsync(PaymentCreateData data, SqlTransaction transaction);
    Task<IEnumerable<OrderItemResponse>> GetOrderItemsAsync(int orderId);
    Task<PaymentResponse?> GetPaymentAsync(int orderId);
}

/// <summary>
/// 訂單建立資料
/// </summary>
public class OrderCreateData
{
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string LightingName { get; set; } = string.Empty;
    public string? BlessingContent { get; set; }
    public decimal TotalAmount { get; set; }
    public string CreatedByWorkstation { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// 付款建立資料
/// </summary>
public class PaymentCreateData
{
    public int OrderId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public decimal AmountReceived { get; set; }
    public string ReceivedByWorkstation { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// 訂單資料存取實作
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(IDbConnectionFactory connectionFactory, ILogger<OrderRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<OrderResponse?> GetByIdAsync(int orderId)
    {
        const string sql = @"
            SELECT
                o.OrderId,
                o.OrderNumber,
                o.CustomerId,
                c.Name AS CustomerName,
                c.Phone AS CustomerPhone,
                o.LightingName,
                o.BlessingContent,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.CreatedByWorkstation,
                o.Notes
            FROM Orders o
            INNER JOIN Customers c ON o.CustomerId = c.CustomerId
            WHERE o.OrderId = @OrderId";

        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QuerySingleOrDefaultAsync<OrderResponse>(sql, new { OrderId = orderId });

        if (order != null)
        {
            order.Items = (await GetOrderItemsAsync(orderId)).ToList();
            order.Payment = await GetPaymentAsync(orderId);
        }

        return order;
    }

    public async Task<OrderResponse?> GetByOrderNumberAsync(string orderNumber)
    {
        const string sql = @"
            SELECT
                o.OrderId,
                o.OrderNumber,
                o.CustomerId,
                c.Name AS CustomerName,
                c.Phone AS CustomerPhone,
                o.LightingName,
                o.BlessingContent,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.CreatedByWorkstation,
                o.Notes
            FROM Orders o
            INNER JOIN Customers c ON o.CustomerId = c.CustomerId
            WHERE o.OrderNumber = @OrderNumber";

        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QuerySingleOrDefaultAsync<OrderResponse>(sql, new { OrderNumber = orderNumber });

        if (order != null)
        {
            order.Items = (await GetOrderItemsAsync(order.OrderId)).ToList();
            order.Payment = await GetPaymentAsync(order.OrderId);
        }

        return order;
    }

    /// <summary>
    /// 產生訂單編號（使用預存程序）
    /// </summary>
    public async Task<string> GenerateOrderNumberAsync(SqlTransaction transaction)
    {
        const string sql = "EXEC sp_GenerateOrderNumber @OrderNumber OUTPUT";

        var parameters = new DynamicParameters();
        parameters.Add("OrderNumber", dbType: System.Data.DbType.String, size: 50, direction: System.Data.ParameterDirection.Output);

        await transaction.Connection!.ExecuteAsync(sql, parameters, transaction);

        var orderNumber = parameters.Get<string>("OrderNumber");
        _logger.LogDebug("產生訂單編號: {OrderNumber}", orderNumber);

        return orderNumber;
    }

    public async Task<int> CreateAsync(OrderCreateData data, SqlTransaction transaction)
    {
        const string sql = @"
            INSERT INTO Orders (CustomerId, OrderNumber, LightingName, BlessingContent, Status, TotalAmount, CreatedByWorkstation, Notes, CreatedAt)
            OUTPUT INSERTED.OrderId
            VALUES (@CustomerId, @OrderNumber, @LightingName, @BlessingContent, 'PENDING', @TotalAmount, @CreatedByWorkstation, @Notes, GETDATE())";

        var orderId = await transaction.Connection!.ExecuteScalarAsync<int>(sql, data, transaction);
        _logger.LogInformation("建立訂單: OrderId={OrderId}, OrderNumber={OrderNumber}", orderId, data.OrderNumber);

        return orderId;
    }

    public async Task<int> CreateOrderItemAsync(int orderId, int slotId, decimal unitPrice, SqlTransaction transaction)
    {
        const string sql = @"
            INSERT INTO OrderItems (OrderId, SlotId, UnitPrice, CreatedAt)
            OUTPUT INSERTED.OrderItemId
            VALUES (@OrderId, @SlotId, @UnitPrice, GETDATE())";

        var itemId = await transaction.Connection!.ExecuteScalarAsync<int>(sql, new
        {
            OrderId = orderId,
            SlotId = slotId,
            UnitPrice = unitPrice
        }, transaction);

        return itemId;
    }

    public async Task<bool> UpdateStatusAsync(int orderId, string status, SqlTransaction transaction)
    {
        const string sql = @"
            UPDATE Orders
            SET Status = @Status, UpdatedAt = GETDATE()
            WHERE OrderId = @OrderId";

        var affected = await transaction.Connection!.ExecuteAsync(sql, new
        {
            OrderId = orderId,
            Status = status
        }, transaction);

        if (affected > 0)
        {
            _logger.LogInformation("訂單狀態更新: OrderId={OrderId}, Status={Status}", orderId, status);
        }

        return affected > 0;
    }

    public async Task<int> CreatePaymentAsync(PaymentCreateData data, SqlTransaction transaction)
    {
        const string sql = @"
            INSERT INTO Payments (OrderId, PaymentMethod, AmountDue, AmountReceived, ChangeAmount, ReceivedByWorkstation, Notes, PaymentTime)
            OUTPUT INSERTED.PaymentId
            VALUES (@OrderId, @PaymentMethod, @AmountDue, @AmountReceived, @AmountReceived - @AmountDue, @ReceivedByWorkstation, @Notes, GETDATE())";

        var paymentId = await transaction.Connection!.ExecuteScalarAsync<int>(sql, data, transaction);
        _logger.LogInformation("建立付款記錄: PaymentId={PaymentId}, OrderId={OrderId}", paymentId, data.OrderId);

        return paymentId;
    }

    public async Task<IEnumerable<OrderItemResponse>> GetOrderItemsAsync(int orderId)
    {
        const string sql = @"
            SELECT
                i.OrderItemId,
                i.SlotId,
                s.SlotNumber,
                t.Name AS LampTypeName,
                s.Zone,
                i.UnitPrice,
                s.Year
            FROM OrderItems i
            INNER JOIN LampSlots s ON i.SlotId = s.SlotId
            INNER JOIN LampTypes t ON s.LampTypeId = t.LampTypeId
            WHERE i.OrderId = @OrderId
            ORDER BY s.Zone, s.SlotNumber";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<OrderItemResponse>(sql, new { OrderId = orderId });
    }

    public async Task<PaymentResponse?> GetPaymentAsync(int orderId)
    {
        const string sql = @"
            SELECT
                PaymentId,
                PaymentMethod,
                AmountDue,
                AmountReceived,
                ChangeAmount,
                PaymentTime,
                ReceivedByWorkstation,
                Notes
            FROM Payments
            WHERE OrderId = @OrderId";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PaymentResponse>(sql, new { OrderId = orderId });
    }
}
