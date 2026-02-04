using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Services.Repositories;
using TempleLampSystem.ViewModels;

namespace TempleLampSystem.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // DbContext
        services.AddDbContext<AppDbContext>();

        // Repositories
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILampRepository, LampRepository>();
        services.AddScoped<ILampOrderRepository, LampOrderRepository>();

        // Services
        services.AddScoped<ILampOrderService, LampOrderService>();
        services.AddScoped<IPrintService, PrintService>();
        services.AddScoped<ISupabaseService, SupabaseService>();
        services.AddScoped<ISyncQueueService, SyncQueueService>();
        services.AddScoped<IConflictResolutionService, ConflictResolutionService>();
        services.AddScoped<IUpdateService, UpdateService>();
        services.AddSingleton<IAutoSyncService, AutoSyncService>();

        // ViewModels
        services.AddScoped<CustomerSearchViewModel>();
        services.AddScoped<LampOrderViewModel>();

        return services;
    }
}
