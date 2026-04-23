using STailor.Core.Domain.Entities;

namespace STailor.Core.Application.Abstractions.Repositories;

public interface ICustomerProfileRepository
{
    Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerProfile>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default);

    Task AddAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default);

    Task UpdateAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default);

    Task RemoveAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default);

    Task<CustomerProfile?> GetByIdWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
