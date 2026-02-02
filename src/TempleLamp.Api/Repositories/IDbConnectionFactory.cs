using Microsoft.Data.SqlClient;

namespace TempleLamp.Api.Repositories;

/// <summary>
/// 資料庫連線工廠介面
/// </summary>
public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}

/// <summary>
/// SQL Server 連線工廠實作
/// </summary>
public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("未設定 DefaultConnection 連線字串");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
