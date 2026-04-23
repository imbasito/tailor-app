using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Core.Application.Abstractions.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);

    Task<Order> AddPaymentAsync(AddPaymentCommand command, CancellationToken cancellationToken = default);

    Task<Order> TransitionStatusAsync(TransitionOrderStatusCommand command, CancellationToken cancellationToken = default);

    Task<Order> ScheduleTrialFittingAsync(
        ScheduleTrialFittingCommand command,
        CancellationToken cancellationToken = default);

    Task<OrderWorkspaceDetail?> GetWorkspaceDetailAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderReminderCandidate>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderWorklistItem>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        bool overdueOnly = false,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        string? searchText = null,
        CancellationToken cancellationToken = default);

    Task<IDictionary<OrderStatus, List<OrderWorklistItem>>> GetOrdersGroupedByStatusAsync(
        bool includeDelivered,
        int maxItems,
        CancellationToken cancellationToken = default);
}
