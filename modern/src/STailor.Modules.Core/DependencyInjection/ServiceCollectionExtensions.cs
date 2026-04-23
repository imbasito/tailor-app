using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Validation;

namespace STailor.Modules.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreModules(this IServiceCollection services)
    {
        services.AddScoped<IMeasurementService, MeasurementService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ILegacyMigrationService, LegacyMigrationService>();
        services.AddScoped<ISyncQueueService, SyncQueueService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddSingleton<ISyncConflictResolver, UpdatedAtSyncConflictResolver>();

        services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
        services.AddScoped<IValidator<UpdateCustomerCommand>, UpdateCustomerCommandValidator>();
        services.AddScoped<IValidator<UpsertBaselineMeasurementsCommand>, UpsertBaselineMeasurementsCommandValidator>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddScoped<IValidator<AddPaymentCommand>, AddPaymentCommandValidator>();
        services.AddScoped<IValidator<TransitionOrderStatusCommand>, TransitionOrderStatusCommandValidator>();
        services.AddScoped<IValidator<ScheduleTrialFittingCommand>, ScheduleTrialFittingCommandValidator>();
        services.AddScoped<IValidator<OutstandingDuesFilter>, OutstandingDuesFilterValidator>();

        return services;
    }
}
