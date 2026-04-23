using FluentValidation;
using System.Text.Json;
using STailor.Core.Application.Abstractions;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Common.Time;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Exceptions;

namespace STailor.Modules.Core.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerProfileRepository _customerProfileRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;
    private readonly ISyncQueueService _syncQueueService;
    private readonly IMeasurementService _measurementService;
    private readonly IValidator<CreateCustomerCommand> _createCustomerValidator;
    private readonly IValidator<UpdateCustomerCommand> _updateCustomerValidator;
    private readonly IValidator<UpsertBaselineMeasurementsCommand> _upsertMeasurementValidator;

    public CustomerService(
        ICustomerProfileRepository customerProfileRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock,
        ISyncQueueService syncQueueService,
        IMeasurementService measurementService,
        IValidator<CreateCustomerCommand> createCustomerValidator,
        IValidator<UpdateCustomerCommand> updateCustomerValidator,
        IValidator<UpsertBaselineMeasurementsCommand> upsertMeasurementValidator)
    {
        _customerProfileRepository = customerProfileRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _clock = clock;
        _syncQueueService = syncQueueService;
        _measurementService = measurementService;
        _createCustomerValidator = createCustomerValidator;
        _updateCustomerValidator = updateCustomerValidator;
        _upsertMeasurementValidator = upsertMeasurementValidator;
    }

    public async Task<CustomerProfile> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        await _createCustomerValidator.ValidateAndThrowAsync(command, cancellationToken);

        var customerProfile = new CustomerProfile(
            command.FullName,
            command.PhoneNumber,
            command.City,
            command.Notes);

        var actor = _currentUserService.GetCurrentUserId();
        var nowUtc = _clock.UtcNow;
        customerProfile.StampCreated(nowUtc, actor);

        await _customerProfileRepository.AddAsync(customerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueCustomerProfileUpsertAsync(customerProfile, cancellationToken);

        return customerProfile;
    }

    public async Task<CustomerProfile> UpdateAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        await _updateCustomerValidator.ValidateAndThrowAsync(command, cancellationToken);

        var customerProfile = await _customerProfileRepository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customerProfile is null)
        {
            throw new DomainRuleViolationException("Customer profile was not found.");
        }

        customerProfile.UpdateIdentity(
            command.FullName,
            command.PhoneNumber,
            command.City,
            command.Notes);
        customerProfile.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());

        await _customerProfileRepository.UpdateAsync(customerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueCustomerProfileUpsertAsync(customerProfile, cancellationToken);

        return customerProfile;
    }

    public async Task<IReadOnlyList<CustomerWorkspaceItem>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new DomainRuleViolationException("Max items must be greater than zero.");
        }

        var customers = await _customerProfileRepository.GetWorklistAsync(
            searchText,
            maxItems,
            cancellationToken);

        if (customers.Count == 0)
        {
            return [];
        }

        var items = new List<CustomerWorkspaceItem>(customers.Count);
        foreach (var customer in customers)
        {
            var orders = await _orderRepository.GetOrdersByCustomerAsync(customer.Id, cancellationToken);
            items.Add(new CustomerWorkspaceItem(
                customer.Id,
                customer.FullName,
                customer.PhoneNumber,
                customer.City,
                customer.Notes,
                orders.Count,
                orders.Sum(order => order.BalanceDue),
                orders.Count == 0 ? null : orders.Max(order => order.ReceivedAtUtc),
                customer.UpdatedAtUtc));
        }

        return items;
    }

    public async Task<CustomerWorkspaceDetail?> GetWorkspaceDetailAsync(
        Guid customerId,
        int recentOrderLimit,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Customer id is required.");
        }

        if (recentOrderLimit <= 0)
        {
            throw new DomainRuleViolationException("Recent order limit must be greater than zero.");
        }

        var customer = await _customerProfileRepository.GetByIdWithHistoryAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var orders = await _orderRepository.GetOrdersByCustomerAsync(customerId, cancellationToken);
        var recentOrders = orders
            .OrderByDescending(order => order.ReceivedAtUtc)
            .Take(recentOrderLimit)
            .Select(order => new CustomerWorkspaceOrder(
                order.Id,
                order.GarmentType,
                order.Status.ToString(),
                order.AmountCharged,
                order.AmountPaid,
                order.BalanceDue,
                order.ReceivedAtUtc,
                order.DueAtUtc,
                order.MeasurementSnapshotJson))
            .ToArray();

        return new CustomerWorkspaceDetail(
            customer.Id,
            customer.FullName,
            customer.PhoneNumber,
            customer.City,
            customer.Notes,
            customer.BaselineMeasurementsJson,
            orders.Sum(order => order.BalanceDue),
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            recentOrders);
    }

    public async Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerProfile = await _customerProfileRepository.GetByIdAsync(customerId, cancellationToken);
        if (customerProfile is null)
        {
            throw new DomainRuleViolationException("Customer profile was not found.");
        }

        var existingOrders = await _orderRepository.GetOrdersByCustomerAsync(customerId, cancellationToken);
        if (existingOrders.Count > 0)
        {
            throw new DomainRuleViolationException("Customer profile cannot be deleted while orders still exist.");
        }

        var nowUtc = _clock.UtcNow;
        customerProfile.StampUpdated(nowUtc, _currentUserService.GetCurrentUserId());
        await _customerProfileRepository.RemoveAsync(customerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueCustomerProfileDeleteAsync(customerProfile, cancellationToken);
    }

    public async Task<CustomerProfile> UpsertBaselineMeasurementsAsync(
        UpsertBaselineMeasurementsCommand command,
        CancellationToken cancellationToken = default)
    {
        await _upsertMeasurementValidator.ValidateAndThrowAsync(command, cancellationToken);

        var customerProfile = await _customerProfileRepository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customerProfile is null)
        {
            throw new DomainRuleViolationException("Customer profile was not found.");
        }

        var existingMeasurements = _measurementService.Deserialize(customerProfile.BaselineMeasurementsJson);
        var namespacedMeasurements = command.Measurements.ToDictionary(
            pair => $"{command.GarmentType}:{pair.Key}".Trim(),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        var mergedMeasurements = _measurementService.MergeMeasurements(existingMeasurements, namespacedMeasurements);

        customerProfile.SetBaselineMeasurements(_measurementService.Serialize(mergedMeasurements));
        customerProfile.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());

        await _customerProfileRepository.UpdateAsync(customerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueCustomerProfileUpsertAsync(customerProfile, cancellationToken);

        return customerProfile;
    }

    private async Task EnqueueCustomerProfileUpsertAsync(
        CustomerProfile customerProfile,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            EntityType = nameof(CustomerProfile),
            EntityId = customerProfile.Id,
            customerProfile.FullName,
            customerProfile.PhoneNumber,
            customerProfile.City,
            customerProfile.UpdatedAtUtc,
        });

        await _syncQueueService.EnqueueAsync(
            entityType: nameof(CustomerProfile),
            entityId: customerProfile.Id,
            operation: "upsert",
            payloadJson: payloadJson,
            entityUpdatedAtUtc: customerProfile.UpdatedAtUtc,
            cancellationToken: cancellationToken);
    }

    private async Task EnqueueCustomerProfileDeleteAsync(
        CustomerProfile customerProfile,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            EntityType = nameof(CustomerProfile),
            EntityId = customerProfile.Id,
            DeletedAtUtc = customerProfile.UpdatedAtUtc,
        });

        await _syncQueueService.EnqueueAsync(
            entityType: nameof(CustomerProfile),
            entityId: customerProfile.Id,
            operation: "delete",
            payloadJson: payloadJson,
            entityUpdatedAtUtc: customerProfile.UpdatedAtUtc,
            cancellationToken: cancellationToken);
    }
}
