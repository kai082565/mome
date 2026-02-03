using Dapper;
using TempleLamp.Api.DTOs.Responses;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 客戶資料存取介面
/// </summary>
public interface ICustomerRepository
{
    Task<IEnumerable<CustomerResponse>> SearchByPhoneAsync(string phone);
    Task<CustomerResponse?> GetByIdAsync(int customerId);
    Task<CustomerResponse?> GetByPhoneAsync(string phone);
    Task<int> CreateAsync(string name, string phone, string? address, string? notes);
    Task<bool> UpdateAsync(int customerId, string name, string? address, string? notes);
}

/// <summary>
/// 客戶資料存取實作
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(IDbConnectionFactory connectionFactory, ILogger<CustomerRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerResponse>> SearchByPhoneAsync(string phone)
    {
        const string sql = @"
            SELECT
                c.CustomerId,
                c.Name,
                c.Phone,
                c.Address,
                c.Notes,
                c.CreatedAt,
                ISNULL(COUNT(o.OrderId), 0) AS TotalOrders
            FROM Customers c
            LEFT JOIN Orders o ON c.CustomerId = o.CustomerId AND o.Status = 'CONFIRMED'
            WHERE c.Phone LIKE @Phone
            GROUP BY c.CustomerId, c.Name, c.Phone, c.Address, c.Notes, c.CreatedAt
            ORDER BY c.Name";

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerResponse>(sql, new { Phone = $"%{phone}%" });
        return results;
    }

    public async Task<CustomerResponse?> GetByIdAsync(int customerId)
    {
        const string sql = @"
            SELECT
                c.CustomerId,
                c.Name,
                c.Phone,
                c.Address,
                c.Notes,
                c.CreatedAt,
                ISNULL(COUNT(o.OrderId), 0) AS TotalOrders
            FROM Customers c
            LEFT JOIN Orders o ON c.CustomerId = o.CustomerId AND o.Status = 'CONFIRMED'
            WHERE c.CustomerId = @CustomerId
            GROUP BY c.CustomerId, c.Name, c.Phone, c.Address, c.Notes, c.CreatedAt";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerResponse>(sql, new { CustomerId = customerId });
    }

    public async Task<CustomerResponse?> GetByPhoneAsync(string phone)
    {
        const string sql = @"
            SELECT
                c.CustomerId,
                c.Name,
                c.Phone,
                c.Address,
                c.Notes,
                c.CreatedAt,
                ISNULL(COUNT(o.OrderId), 0) AS TotalOrders
            FROM Customers c
            LEFT JOIN Orders o ON c.CustomerId = o.CustomerId AND o.Status = 'CONFIRMED'
            WHERE c.Phone = @Phone
            GROUP BY c.CustomerId, c.Name, c.Phone, c.Address, c.Notes, c.CreatedAt";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerResponse>(sql, new { Phone = phone });
    }

    public async Task<int> CreateAsync(string name, string phone, string? address, string? notes)
    {
        const string sql = @"
            INSERT INTO Customers (Name, Phone, Address, Notes, CreatedAt)
            OUTPUT INSERTED.CustomerId
            VALUES (@Name, @Phone, @Address, @Notes, GETDATE())";

        using var connection = _connectionFactory.CreateConnection();
        var customerId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            Phone = phone,
            Address = address,
            Notes = notes
        });

        _logger.LogInformation("建立客戶成功: CustomerId={CustomerId}, Phone={Phone}", customerId, phone);
        return customerId;
    }

    public async Task<bool> UpdateAsync(int customerId, string name, string? address, string? notes)
    {
        const string sql = @"
            UPDATE Customers
            SET Name = @Name, Address = @Address, Notes = @Notes, UpdatedAt = GETDATE()
            WHERE CustomerId = @CustomerId";

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            CustomerId = customerId,
            Name = name,
            Address = address,
            Notes = notes
        });

        return affected > 0;
    }
}
