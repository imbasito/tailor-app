using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of order repository.
/// </summary>
public class EfOrderRepository : IOrderRepository
{
    private readonly LocalTailorDbContext _context;

    public EfOrderRepository(LocalTailorDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Where(o => o.Status != OrderStatus.Delivered)
            .ToListAsync(cancellationToken);

        return orders
            .Where(o => o.DueAtUtc <= dueOnOrBeforeUtc)
            .OrderBy(o => o.DueAtUtc)
            .Take(maxItems)
            .ToList();
    }

    public async Task<IReadOnlyList<Order>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.AsQueryable();

        if (!includeDelivered)
        {
            query = query.Where(o => o.Status != OrderStatus.Delivered);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(o => o.Status == statusFilter.Value);
        }

        var orders = await query.ToListAsync(cancellationToken);

        if (dueOnOrBeforeUtc.HasValue)
        {
            orders = orders
                .Where(o => o.DueAtUtc <= dueOnOrBeforeUtc.Value)
                .ToList();
        }

        return orders
            .OrderBy(o => o.DueAtUtc)
            .Take(maxItems)
            .ToList();
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        var orderEntry = _context.Entry(order);
        if (orderEntry.State == EntityState.Detached)
        {
            _context.Attach(order);
            orderEntry = _context.Entry(order);
            orderEntry.State = EntityState.Modified;
        }

        foreach (var payment in order.Payments)
        {
            var paymentEntry = _context.Entry(payment);
            var existsInStore = _context.Payments
                .AsNoTracking()
                .Any(existingPayment => existingPayment.Id == payment.Id);

            if (!existsInStore)
            {
                paymentEntry.State = EntityState.Added;
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Remove(order);
        return Task.CompletedTask;
    }

    // Reporting queries
    public async Task<IReadOnlyList<Order>> GetOrdersReceivedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Include(o => o.Payments)
            .ToListAsync(cancellationToken);

        return orders
            .Where(o => o.ReceivedAtUtc >= from && o.ReceivedAtUtc <= to)
            .OrderByDescending(o => o.ReceivedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<Order>> GetOrdersWithBalanceDueAsync(
        decimal? minBalanceDue,
        decimal? maxBalanceDue,
        string? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Payments)
            .Where(o => o.AmountCharged - o.AmountPaid > 0)
            .AsQueryable();

        // Filter by minimum balance due
        if (minBalanceDue.HasValue)
        {
            query = query.Where(o => (o.AmountCharged - o.AmountPaid) >= minBalanceDue.Value);
        }

        // Filter by maximum balance due
        if (maxBalanceDue.HasValue)
        {
            query = query.Where(o => (o.AmountCharged - o.AmountPaid) <= maxBalanceDue.Value);
        }

        // Filter by status
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            if (Enum.TryParse<OrderStatus>(statusFilter, true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
            }
            else if (TryParseAlias(statusFilter, out var aliasStatus))
            {
                query = query.Where(o => o.Status == aliasStatus);
            }
        }

        return await query
            .OrderByDescending(o => o.AmountCharged - o.AmountPaid)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Include(o => o.Payments)
            .Where(o => o.CustomerProfileId == customerId)
            .ToListAsync(cancellationToken);

        return orders
            .OrderByDescending(o => o.ReceivedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<Order>> GetOrdersForDeliveryWindowAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var from = new DateTimeOffset(fromDate, TimeSpan.Zero);
        var to = new DateTimeOffset(toDate.AddDays(1).AddTicks(-1), TimeSpan.Zero);

        var orders = await _context.Orders
            .Include(o => o.Payments)
            .ToListAsync(cancellationToken);

        return orders
            .Where(o => o.DueAtUtc >= from && o.DueAtUtc <= to)
            .OrderBy(o => o.DueAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<Order>> GetOrdersForOperationsReportAsync(
        bool includeDelivered,
        OrderStatus? statusFilter = null,
        DateTimeOffset? receivedFromUtc = null,
        DateTimeOffset? receivedToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Payments)
            .AsQueryable();

        if (!includeDelivered)
        {
            query = query.Where(o => o.Status != OrderStatus.Delivered);
        }

        if (statusFilter is not null)
        {
            query = query.Where(o => o.Status == statusFilter.Value);
        }

        var orders = await query.ToListAsync(cancellationToken);

        return orders
            .Where(o => receivedFromUtc is null || o.ReceivedAtUtc >= receivedFromUtc.Value)
            .Where(o => receivedToUtc is null || o.ReceivedAtUtc <= receivedToUtc.Value)
            .OrderByDescending(o => o.ReceivedAtUtc)
            .ToList();
    }

    private static bool TryParseAlias(string alias, out OrderStatus status)
    {
        var normalized = alias.ToLowerInvariant().Replace(" ", "").Replace("/", "").Replace("-", "");
        status = normalized switch
        {
            "new" => OrderStatus.New,
            "inprogress" or "inprogress" => OrderStatus.InProgress,
            "trialfitting" or "trial" or "fitting" => OrderStatus.TrialFitting,
            "rework" => OrderStatus.Rework,
            "ready" => OrderStatus.Ready,
            "delivered" => OrderStatus.Delivered,
            _ => OrderStatus.New
        };
        return normalized is "new" or "inprogress" or "trialfitting" or "trial" or "fitting"
            or "rework" or "ready" or "delivered";
    }
}
