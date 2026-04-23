using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;

namespace STailor.Core.Application.Abstractions.Services;

public interface ICustomerService
{
    Task<CustomerProfile> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default);

    Task<CustomerProfile> UpdateAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerWorkspaceItem>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default);

    Task<CustomerWorkspaceDetail?> GetWorkspaceDetailAsync(
        Guid customerId,
        int recentOrderLimit,
        CancellationToken cancellationToken = default);

    Task<CustomerProfile> UpsertBaselineMeasurementsAsync(UpsertBaselineMeasurementsCommand command, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default);
}
