using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _store = new();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<IReadOnlyList<Order>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(order =>
                order.DueAtUtc <= dueOnOrBeforeUtc
                && order.BalanceDue > 0m
                && order.Status != OrderStatus.Delivered)
            .OrderBy(order => order.DueAtUtc)
            .Take(maxItems)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _store.Values.AsEnumerable();

        if (!includeDelivered)
        {
            query = query.Where(order => order.Status != OrderStatus.Delivered);
        }

        if (statusFilter is not null)
        {
            query = query.Where(order => order.Status == statusFilter.Value);
        }

        if (dueOnOrBeforeUtc is not null)
        {
            query = query.Where(order => order.DueAtUtc <= dueOnOrBeforeUtc.Value);
        }

        var items = query
            .OrderBy(order => order.DueAtUtc)
            .Take(maxItems)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store.Remove(order.Id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Order>> GetOrdersReceivedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(order => order.ReceivedAtUtc >= from && order.ReceivedAtUtc <= to)
            .OrderBy(order => order.ReceivedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> GetOrdersWithBalanceDueAsync(
        decimal? minBalanceDue,
        decimal? maxBalanceDue,
        string? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var query = _store.Values.Where(order => order.BalanceDue > 0m);

        if (minBalanceDue is not null)
        {
            query = query.Where(order => order.BalanceDue >= minBalanceDue.Value);
        }

        if (maxBalanceDue is not null)
        {
            query = query.Where(order => order.BalanceDue <= maxBalanceDue.Value);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(order =>
                string.Equals(order.Status.ToString(), statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        var items = query
            .OrderByDescending(order => order.BalanceDue)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(order => order.CustomerProfileId == customerId)
            .OrderByDescending(order => order.ReceivedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> GetOrdersForDeliveryWindowAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = new DateTimeOffset(fromDate.Date, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(toDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

        var items = _store.Values
            .Where(order => order.DueAtUtc >= fromUtc && order.DueAtUtc <= toUtc)
            .OrderBy(order => order.DueAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> GetOrdersForOperationsReportAsync(
        bool includeDelivered,
        OrderStatus? statusFilter = null,
        DateTimeOffset? receivedFromUtc = null,
        DateTimeOffset? receivedToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _store.Values.AsEnumerable();

        if (!includeDelivered)
        {
            query = query.Where(order => order.Status != OrderStatus.Delivered);
        }

        if (statusFilter is not null)
        {
            query = query.Where(order => order.Status == statusFilter.Value);
        }

        if (receivedFromUtc is not null)
        {
            query = query.Where(order => order.ReceivedAtUtc >= receivedFromUtc.Value);
        }

        if (receivedToUtc is not null)
        {
            query = query.Where(order => order.ReceivedAtUtc <= receivedToUtc.Value);
        }

        var items = query
            .OrderByDescending(order => order.ReceivedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<Order>>(items);
    }
}
