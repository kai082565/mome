using TempleLamp.Api.Repositories;
using TempleLamp.Api.Services;

namespace TempleLamp.Api.Extensions;

/// <summary>
/// 服務註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Repository 層服務
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILampSlotRepository, LampSlotRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        return services;
    }

    /// <summary>
    /// 註冊 Service 層服務
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ILampSlotService, LampSlotService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
