using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Domain.Entities;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of customer profile repository.
/// </summary>
public class EfCustomerProfileRepository : ICustomerProfileRepository
{
    private readonly LocalTailorDbContext _context;

    public EfCustomerProfileRepository(LocalTailorDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CustomerProfiles.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerProfile>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CustomerProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmed = searchText.Trim();
            query = query.Where(customer =>
                customer.FullName.Contains(trimmed)
                || customer.PhoneNumber.Contains(trimmed)
                || customer.City.Contains(trimmed));
        }

        var customers = await query.ToListAsync(cancellationToken);

        return customers
            .OrderByDescending(customer => customer.UpdatedAtUtc)
            .ThenBy(customer => customer.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();
    }

    public async Task AddAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        await _context.CustomerProfiles.AddAsync(customerProfile, cancellationToken);
    }

    public Task UpdateAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        _context.CustomerProfiles.Update(customerProfile);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(CustomerProfile customerProfile, CancellationToken cancellationToken = default)
    {
        _context.CustomerProfiles.Remove(customerProfile);
        return Task.CompletedTask;
    }

    public async Task<CustomerProfile?> GetByIdWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // For now, just return the customer profile.
        // In a more complex scenario, this could include order history
        return await _context.CustomerProfiles.FindAsync(new object[] { id }, cancellationToken);
    }
}
