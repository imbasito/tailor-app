using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Domain.Entities;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class InMemoryCustomerProfileRepository : ICustomerProfileRepository
{
    private readonly Dictionary<Guid, CustomerProfile> _store = new();

    public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var customer);
        return Task.FromResult(customer);
    }

    public Task<IReadOnlyList<CustomerProfile>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var query = _store.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmed = searchText.Trim();
            query = query.Where(customer =>
                customer.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || customer.PhoneNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || customer.City.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        var items = query
            .OrderByDescending(customer => customer.UpdatedAtUtc)
            .ThenBy(customer => customer.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        return Task.FromResult<IReadOnlyList<CustomerProfile>>(items);
    }

    public Task AddAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        _store[customerProfile.Id] = customerProfile;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        _store[customerProfile.Id] = customerProfile;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        _store.Remove(customerProfile.Id);
        return Task.CompletedTask;
    }

    public Task<CustomerProfile?> GetByIdWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var customer);
        return Task.FromResult(customer);
    }
}
