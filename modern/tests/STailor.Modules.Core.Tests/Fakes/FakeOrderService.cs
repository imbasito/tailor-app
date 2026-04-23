using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class FakeOrderService : IOrderService
{
    public OrderWorkspaceDetail? DetailResult { get; set; }

    public List<CreateOrderCommand> CreateCommands { get; } = [];

    public List<Order> CreatedOrders { get; } = [];

    public bool IgnoreInitialDeposit { get; set; }

    public Task<Order> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        CreateCommands.Add(command);

        var receivedAtUtc = command.DueAtUtc.AddDays(-1);
        var order = new Order(
            command.CustomerId,
            command.GarmentType,
            "{\"Migrated\":1}",
            command.AmountCharged,
            receivedAtUtc,
            command.DueAtUtc);

        if (command.InitialDeposit > 0 && !IgnoreInitialDeposit)
        {
            order.ApplyPayment(command.InitialDeposit, receivedAtUtc, "Legacy initial deposit");
        }

        CreatedOrders.Add(order);
        return Task.FromResult(order);
    }

    public Task<Order> AddPaymentAsync(
        AddPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Add payment is not used by this fake in migration tests.");
    }

    public Task<Order> TransitionStatusAsync(
        TransitionOrderStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Status transitions are not used by this fake in migration tests.");
    }

    public Task<Order> ScheduleTrialFittingAsync(
        ScheduleTrialFittingCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Trial scheduling is not used by this fake in migration tests.");
    }

    public Task<OrderWorkspaceDetail?> GetWorkspaceDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DetailResult);
    }

    public Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Delete is not used by this fake in migration tests.");
    }

    public Task<IReadOnlyList<OrderReminderCandidate>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OrderReminderCandidate>>([]);
    }

    public Task<IReadOnlyList<OrderWorklistItem>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        bool overdueOnly = false,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OrderWorklistItem>>([]);
    }

    public Task<IDictionary<OrderStatus, List<OrderWorklistItem>>> GetOrdersGroupedByStatusAsync(
        bool includeDelivered,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDictionary<OrderStatus, List<OrderWorklistItem>>>(
            new Dictionary<OrderStatus, List<OrderWorklistItem>>());
    }
}
