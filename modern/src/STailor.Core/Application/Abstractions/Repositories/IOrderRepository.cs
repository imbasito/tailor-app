using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Core.Application.Abstractions.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    Task RemoveAsync(Order order, CancellationToken cancellationToken = default);

    // Reporting queries
    Task<IReadOnlyList<Order>> GetOrdersReceivedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersWithBalanceDueAsync(
        decimal? minBalanceDue,
        decimal? maxBalanceDue,
        string? statusFilter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersForDeliveryWindowAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersForOperationsReportAsync(
        bool includeDelivered,
        OrderStatus? statusFilter = null,
        DateTimeOffset? receivedFromUtc = null,
        DateTimeOffset? receivedToUtc = null,
        CancellationToken cancellationToken = default);
}
