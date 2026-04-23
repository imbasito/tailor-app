using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STailor.Core.Application.Abstractions;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Common.Time;
using STailor.Infrastructure.Persistence;
using STailor.Infrastructure.Repositories;
using STailor.Infrastructure.Services;

namespace STailor.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string localConnectionString,
        string centralConnectionString)
    {
        if (string.IsNullOrWhiteSpace(localConnectionString))
        {
            throw new ArgumentException("Local connection string is required.", nameof(localConnectionString));
        }

        if (string.IsNullOrWhiteSpace(centralConnectionString))
        {
            throw new ArgumentException("Central connection string is required.", nameof(centralConnectionString));
        }

        services.AddDbContext<LocalTailorDbContext>(options =>
            options.UseSqlite(localConnectionString));

        services.AddDbContext<CentralTailorDbContext>(options =>
            options.UseNpgsql(centralConnectionString));

        services.AddScoped<ICustomerProfileRepository, EfCustomerProfileRepository>();
        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<ISyncQueueRepository, EfSyncQueueRepository>();
        services.AddScoped<ISyncQueueDispatcher, CentralSyncQueueDispatcher>();
        services.AddScoped<ICentralSyncPullService, CentralSyncPullService>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        services.AddSingleton<ILegacyMigrationMapper, LegacyMigrationMapper>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
