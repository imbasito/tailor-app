using FluentValidation;
using System.Text.Json;
using STailor.Core.Application.Abstractions;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Common.Time;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;

namespace STailor.Modules.Core.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerProfileRepository _customerProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;
    private readonly ISyncQueueService _syncQueueService;
    private readonly IMeasurementService _measurementService;
    private readonly IValidator<CreateOrderCommand> _createOrderValidator;
    private readonly IValidator<AddPaymentCommand> _addPaymentValidator;
    private readonly IValidator<TransitionOrderStatusCommand> _transitionValidator;
    private readonly IValidator<ScheduleTrialFittingCommand> _scheduleTrialValidator;

    public OrderService(
        IOrderRepository orderRepository,
        ICustomerProfileRepository customerProfileRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock,
        ISyncQueueService syncQueueService,
        IMeasurementService measurementService,
        IValidator<CreateOrderCommand> createOrderValidator,
        IValidator<AddPaymentCommand> addPaymentValidator,
        IValidator<TransitionOrderStatusCommand> transitionValidator,
        IValidator<ScheduleTrialFittingCommand> scheduleTrialValidator)
    {
        _orderRepository = orderRepository;
        _customerProfileRepository = customerProfileRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _clock = clock;
        _syncQueueService = syncQueueService;
        _measurementService = measurementService;
        _createOrderValidator = createOrderValidator;
        _addPaymentValidator = addPaymentValidator;
        _transitionValidator = transitionValidator;
        _scheduleTrialValidator = scheduleTrialValidator;
    }

    public async Task<Order> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await _createOrderValidator.ValidateAndThrowAsync(command, cancellationToken);

        var customerProfile = await _customerProfileRepository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customerProfile is null)
        {
            throw new DomainRuleViolationException("Customer profile was not found.");
        }

        var measurementSnapshot = BuildMeasurementSnapshot(
            customerProfile.BaselineMeasurementsJson,
            command.GarmentType,
            command.OverrideMeasurements);

        var nowUtc = _clock.UtcNow;
        var actor = _currentUserService.GetCurrentUserId();

        var order = new Order(
            customerProfileId: command.CustomerId,
            garmentType: command.GarmentType,
            measurementSnapshotJson: measurementSnapshot,
            amountCharged: command.AmountCharged,
            receivedAtUtc: nowUtc,
            dueAtUtc: command.DueAtUtc,
            photoAttachmentsJson: SerializeAttachments(command.PhotoAttachments));

        order.StampCreated(nowUtc, actor);

        if (command.InitialDeposit > 0)
        {
            var payment = order.ApplyPayment(command.InitialDeposit, nowUtc, "Initial deposit");
            payment.StampCreated(nowUtc, actor);
        }

        if (command.TrialScheduledAtUtc is not null)
        {
            var scheduleStatus = string.IsNullOrWhiteSpace(command.TrialScheduleStatus)
                ? "Scheduled"
                : command.TrialScheduleStatus;

            order.ScheduleTrial(command.TrialScheduledAtUtc.Value, scheduleStatus);
            if (command.ApplyTrialStatusTransition)
            {
                if (order.Status == OrderStatus.New)
                {
                    order.TransitionTo(OrderStatus.InProgress);
                }

                if (order.Status == OrderStatus.InProgress)
                {
                    order.TransitionTo(OrderStatus.TrialFitting);
                }
            }
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueOrderUpsertAsync(order, cancellationToken);

        return order;
    }

    public async Task<Order> AddPaymentAsync(
        AddPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        await _addPaymentValidator.ValidateAndThrowAsync(command, cancellationToken);

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            throw new DomainRuleViolationException("Order was not found.");
        }

        var nowUtc = _clock.UtcNow;
        var actor = _currentUserService.GetCurrentUserId();
        var paidAtUtc = command.PaidAtUtc ?? nowUtc;

        if (command.Amount > order.BalanceDue)
        {
            throw new DomainRuleViolationException("Payment exceeds current balance due.");
        }

        var payment = order.ApplyPayment(command.Amount, paidAtUtc, command.Note);
        payment.StampCreated(nowUtc, actor);

        order.StampUpdated(nowUtc, actor);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueOrderUpsertAsync(order, cancellationToken);

        return order;
    }

    public async Task<Order> TransitionStatusAsync(
        TransitionOrderStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        await _transitionValidator.ValidateAndThrowAsync(command, cancellationToken);

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            throw new DomainRuleViolationException("Order was not found.");
        }

        order.TransitionTo(command.TargetStatus);
        order.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueOrderUpsertAsync(order, cancellationToken);

        return order;
    }

    public async Task<Order> ScheduleTrialFittingAsync(
        ScheduleTrialFittingCommand command,
        CancellationToken cancellationToken = default)
    {
        await _scheduleTrialValidator.ValidateAndThrowAsync(command, cancellationToken);

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            throw new DomainRuleViolationException("Order was not found.");
        }

        order.ScheduleTrial(command.TrialAtUtc, command.ScheduleStatus);

        if (command.ApplyTrialStatusTransition)
        {
            if (order.Status == OrderStatus.New)
            {
                order.TransitionTo(OrderStatus.InProgress);
            }

            if (order.Status == OrderStatus.InProgress)
            {
                order.TransitionTo(OrderStatus.TrialFitting);
            }
        }

        order.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());
        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueOrderUpsertAsync(order, cancellationToken);

        return order;
    }

    public async Task<OrderWorkspaceDetail?> GetWorkspaceDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Order id is required.");
        }

        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var customer = await _customerProfileRepository.GetByIdAsync(order.CustomerProfileId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        return new OrderWorkspaceDetail(
            order.Id,
            customer.Id,
            customer.FullName,
            customer.PhoneNumber,
            customer.City,
            order.GarmentType,
            order.Status.ToString(),
            order.AmountCharged,
            order.AmountPaid,
            order.BalanceDue,
            order.ReceivedAtUtc,
            order.DueAtUtc,
            order.MeasurementSnapshotJson,
            order.PhotoAttachmentsJson,
            order.TrialScheduledAtUtc,
            order.TrialScheduleStatus,
            order.Payments
                .OrderByDescending(payment => payment.PaidAtUtc)
                .Select(payment => new OrderPaymentHistoryItem(
                    payment.Id,
                    payment.Amount,
                    payment.PaidAtUtc,
                    payment.Note))
                .ToArray());
    }

    public async Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new DomainRuleViolationException("Order was not found.");
        }

        order.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());
        await _orderRepository.RemoveAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueOrderDeleteAsync(order, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderReminderCandidate>> GetReminderCandidatesAsync(
        DateTimeOffset dueOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new DomainRuleViolationException("Max items must be greater than zero.");
        }

        var orders = await _orderRepository.GetReminderCandidatesAsync(
            dueOnOrBeforeUtc,
            maxItems,
            cancellationToken);

        if (orders.Count == 0)
        {
            return [];
        }

        var customers = new Dictionary<Guid, CustomerProfile>();
        var candidates = new List<OrderReminderCandidate>(orders.Count);

        foreach (var order in orders)
        {
            if (!customers.TryGetValue(order.CustomerProfileId, out var customer))
            {
                customer = await _customerProfileRepository.GetByIdAsync(order.CustomerProfileId, cancellationToken);
                if (customer is null)
                {
                    continue;
                }

                customers[order.CustomerProfileId] = customer;
            }

            candidates.Add(new OrderReminderCandidate(
                order.Id,
                customer.Id,
                customer.FullName,
                customer.PhoneNumber,
                order.GarmentType,
                order.Status.ToString(),
                order.AmountCharged,
                order.AmountPaid,
                order.BalanceDue,
                order.DueAtUtc));
        }

        return candidates;
    }

    public async Task<IReadOnlyList<OrderWorklistItem>> GetWorklistAsync(
        bool includeDelivered,
        int maxItems,
        OrderStatus? statusFilter = null,
        bool overdueOnly = false,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new DomainRuleViolationException("Max items must be greater than zero.");
        }

        var effectiveDueCutoffUtc = dueOnOrBeforeUtc;
        if (overdueOnly)
        {
            var nowUtc = _clock.UtcNow;
            effectiveDueCutoffUtc = effectiveDueCutoffUtc is null || effectiveDueCutoffUtc > nowUtc
                ? nowUtc
                : effectiveDueCutoffUtc;
        }

        var orders = await _orderRepository.GetWorklistAsync(
            includeDelivered,
            string.IsNullOrWhiteSpace(searchText) ? maxItems : 500,
            statusFilter,
            effectiveDueCutoffUtc,
            cancellationToken);

        if (orders.Count == 0)
        {
            return [];
        }

        var customers = new Dictionary<Guid, CustomerProfile>();
        var items = new List<OrderWorklistItem>(orders.Count);

        foreach (var order in orders)
        {
            if (!customers.TryGetValue(order.CustomerProfileId, out var customer))
            {
                customer = await _customerProfileRepository.GetByIdAsync(order.CustomerProfileId, cancellationToken);
                if (customer is null)
                {
                    continue;
                }

                customers[order.CustomerProfileId] = customer;
            }

            items.Add(new OrderWorklistItem(
                order.Id,
                customer.Id,
                customer.FullName,
                customer.PhoneNumber,
                customer.City,
                order.GarmentType,
                order.Status.ToString(),
                order.AmountCharged,
                order.AmountPaid,
                order.BalanceDue,
                order.ReceivedAtUtc,
                order.DueAtUtc,
                order.TrialScheduledAtUtc,
                order.TrialScheduleStatus));
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        var normalizedSearch = searchText.Trim();

        return items
            .Where(item =>
                item.OrderId.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.CustomerId.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.CustomerName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.PhoneNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.City.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.GarmentType.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.Status.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.AmountCharged.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.AmountPaid.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.BalanceDue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Take(maxItems)
            .ToArray();
    }

    public async Task<IDictionary<OrderStatus, List<OrderWorklistItem>>> GetOrdersGroupedByStatusAsync(
        bool includeDelivered,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var worklist = await GetWorklistAsync(
            includeDelivered,
            maxItems,
            statusFilter: null,
            overdueOnly: false,
            dueOnOrBeforeUtc: null,
            searchText: null,
            cancellationToken: cancellationToken);

        return worklist
            .OrderBy(item => Enum.TryParse<OrderStatus>(item.Status, out var status) ? status : OrderStatus.New)
            .ThenBy(item => item.CustomerName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(item => Enum.TryParse<OrderStatus>(item.Status, out var status) ? status : OrderStatus.New)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private string BuildMeasurementSnapshot(
        string baselineMeasurementsJson,
        string garmentType,
        IReadOnlyDictionary<string, decimal>? overrides)
    {
        var allMeasurements = _measurementService.Deserialize(baselineMeasurementsJson);
        var garmentPrefix = $"{garmentType}:";

        var garmentBaseline = allMeasurements
            .Where(pair => pair.Key.StartsWith(garmentPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                pair => pair.Key[garmentPrefix.Length..],
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        var merged = _measurementService.MergeMeasurements(garmentBaseline, overrides);

        if (merged.Count == 0)
        {
            throw new DomainRuleViolationException(
                $"No measurements are available for garment type '{garmentType}'.");
        }

        return _measurementService.Serialize(merged);
    }

    private static string SerializeAttachments(IReadOnlyList<CreateOrderPhotoAttachmentCommand>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return "[]";
        }

        var cleaned = attachments
            .Where(item => !string.IsNullOrWhiteSpace(item.ResourcePath))
            .Select(item => new
            {
                FileName = string.IsNullOrWhiteSpace(item.FileName) ? "attachment" : item.FileName.Trim(),
                ResourcePath = item.ResourcePath.Trim(),
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
            })
            .ToArray();

        return JsonSerializer.Serialize(cleaned);
    }

    private async Task EnqueueOrderUpsertAsync(Order order, CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            EntityType = nameof(Order),
            EntityId = order.Id,
            order.CustomerProfileId,
            order.Status,
            order.AmountCharged,
            order.AmountPaid,
            order.BalanceDue,
            order.UpdatedAtUtc,
        });

        await _syncQueueService.EnqueueAsync(
            entityType: nameof(Order),
            entityId: order.Id,
            operation: "upsert",
            payloadJson: payloadJson,
            entityUpdatedAtUtc: order.UpdatedAtUtc,
            cancellationToken: cancellationToken);
    }

    private async Task EnqueueOrderDeleteAsync(Order order, CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            EntityType = nameof(Order),
            EntityId = order.Id,
            DeletedAtUtc = order.UpdatedAtUtc,
        });

        await _syncQueueService.EnqueueAsync(
            entityType: nameof(Order),
            entityId: order.Id,
            operation: "delete",
            payloadJson: payloadJson,
            entityUpdatedAtUtc: order.UpdatedAtUtc,
            cancellationToken: cancellationToken);
    }
}
