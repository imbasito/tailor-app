using STailor.Core.Application.Commands;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Tests.Fakes;
using STailor.Modules.Core.Validation;

namespace STailor.Modules.Core.Tests.Services;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_UsesSnapshotAndInitialDeposit()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40,\"Suit:Waist\":35}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(
                customer.Id,
                "Suit",
                new Dictionary<string, decimal> { ["Waist"] = 36m },
                5000m,
                1000m,
                clock.UtcNow.AddDays(7)));

        Assert.Equal(5000m, order.AmountCharged);
        Assert.Equal(1000m, order.AmountPaid);
        Assert.Equal(4000m, order.BalanceDue);
        Assert.Contains("Waist", order.MeasurementSnapshotJson);
    }

    [Fact]
    public async Task TransitionStatusAsync_RejectsInvalidTransition()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2000m, 0m, clock.UtcNow.AddDays(3)));

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.TransitionStatusAsync(
                new TransitionOrderStatusCommand(order.Id, OrderStatus.Delivered)));

        Assert.Contains("Invalid", exception.Message);
    }

    [Fact]
    public async Task TransitionStatusAsync_DisallowsNoOpAndSkippingAndBackward()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Test Customer", "+251900000099", "Test City");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2000m, 0m, clock.UtcNow.AddDays(3)));

        var noOp = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.New)));
        Assert.Contains("already in status", noOp.Message);

        var skip = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.TrialFitting)));
        Assert.Contains("Invalid sequential transition", skip.Message);

        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.InProgress));
        Assert.Equal(OrderStatus.InProgress, order.Status);

        var backward = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.New)));
        Assert.Contains("Invalid sequential transition", backward.Message);
    }

    [Fact]
    public async Task AddPaymentAsync_UpdatesPaidAndBalance()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(5)));

        var updated = await service.AddPaymentAsync(
            new AddPaymentCommand(order.Id, 750m, null, "Second payment"));

        Assert.Equal(1250m, updated.AmountPaid);
        Assert.Equal(1250m, updated.BalanceDue);
    }

    [Fact]
    public async Task AddPaymentAsync_RejectsOverpayment()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(5)));

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.AddPaymentAsync(new AddPaymentCommand(order.Id, 2500m, null, "Too much")));

        Assert.Contains("exceeds current balance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWorkspaceDetailAsync_ReturnsCustomerAndPaymentHistory()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(5)));

        var updated = await service.AddPaymentAsync(
            new AddPaymentCommand(order.Id, 750m, clock.UtcNow.AddDays(1), "Second payment"));

        var detail = await service.GetWorkspaceDetailAsync(order.Id);

        Assert.NotNull(detail);
        Assert.Equal(customer.FullName, detail!.CustomerName);
        Assert.Equal(updated.Id, detail.OrderId);
        Assert.Equal(2, detail.Payments.Count);
        Assert.Equal(1250m, detail.AmountPaid);
    }

    [Fact]
    public async Task TransitionStatusAsync_SequentialWorkflow_ReachesDelivered()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251922222222", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2000m, 0m, clock.UtcNow.AddDays(3)));

        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.InProgress));
        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.TrialFitting));
        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.Rework));
        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.Ready));
        order = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(order.Id, OrderStatus.Delivered));

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public async Task GetReminderCandidatesAsync_ReturnsOnlyDueOutstandingOrdersWithCustomerDetails()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer1 = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251900000001", "Harar");
        customer1.StampCreated(clock.UtcNow, "owner-admin");
        customer1.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer1);

        var customer2 = new STailor.Core.Domain.Entities.CustomerProfile("Samir Ali", "+251900000002", "Dire Dawa");
        customer2.StampCreated(clock.UtcNow, "owner-admin");
        customer2.SetBaselineMeasurements("{\"Suit:Chest\":38}");
        await customerRepository.AddAsync(customer2);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        _ = await service.CreateOrderAsync(
            new CreateOrderCommand(customer1.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(4)));

        _ = await service.CreateOrderAsync(
            new CreateOrderCommand(customer2.Id, "Suit", null, 2000m, 2000m, clock.UtcNow.AddDays(2)));

        var reminders = await service.GetReminderCandidatesAsync(clock.UtcNow.AddDays(7), 10);

        var candidate = Assert.Single(reminders);
        Assert.Equal(customer1.Id, candidate.CustomerId);
        Assert.Equal("Amina Noor", candidate.CustomerName);
        Assert.Equal("+251900000001", candidate.PhoneNumber);
        Assert.Equal(2000m, candidate.BalanceDue);
    }

    [Fact]
    public async Task GetWorklistAsync_WithIncludeDeliveredFalse_ExcludesDeliveredOrders()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251900000003", "Harar");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var activeOrder = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(4)));

        var deliveredOrder = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 1800m, 300m, clock.UtcNow.AddDays(2)));
        deliveredOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(deliveredOrder.Id, OrderStatus.InProgress));
        deliveredOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(deliveredOrder.Id, OrderStatus.TrialFitting));
        deliveredOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(deliveredOrder.Id, OrderStatus.Rework));
        deliveredOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(deliveredOrder.Id, OrderStatus.Ready));
        _ = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(deliveredOrder.Id, OrderStatus.Delivered));

        var activeOnly = await service.GetWorklistAsync(includeDelivered: false, maxItems: 10);
        var includingDelivered = await service.GetWorklistAsync(includeDelivered: true, maxItems: 10);

        var activeItem = Assert.Single(activeOnly);
        Assert.Equal(activeOrder.Id, activeItem.OrderId);
        Assert.Equal("Muna Ali", activeItem.CustomerName);
        Assert.Equal("Harar", activeItem.City);
        Assert.Equal(2000m, activeItem.BalanceDue);

        Assert.Equal(2, includingDelivered.Count);
        Assert.Contains(includingDelivered, item => item.Status == OrderStatus.Delivered.ToString());
    }

    [Fact]
    public async Task GetWorklistAsync_WithStatusAndDueFilters_ReturnsMatchingOrdersOnly()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Muna Ali", "+251900000003", "Harar");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var dueSoonReadyOrder = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(2)));
        dueSoonReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueSoonReadyOrder.Id, OrderStatus.InProgress));
        dueSoonReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueSoonReadyOrder.Id, OrderStatus.TrialFitting));
        dueSoonReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueSoonReadyOrder.Id, OrderStatus.Rework));
        _ = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueSoonReadyOrder.Id, OrderStatus.Ready));

        var dueLaterReadyOrder = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 1800m, 300m, clock.UtcNow.AddDays(10)));
        dueLaterReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueLaterReadyOrder.Id, OrderStatus.InProgress));
        dueLaterReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueLaterReadyOrder.Id, OrderStatus.TrialFitting));
        dueLaterReadyOrder = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueLaterReadyOrder.Id, OrderStatus.Rework));
        _ = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(dueLaterReadyOrder.Id, OrderStatus.Ready));

        var filtered = await service.GetWorklistAsync(
            includeDelivered: true,
            maxItems: 10,
            statusFilter: OrderStatus.Ready,
            overdueOnly: false,
            dueOnOrBeforeUtc: clock.UtcNow.AddDays(4));

        var item = Assert.Single(filtered);
        Assert.Equal(dueSoonReadyOrder.Id, item.OrderId);
        Assert.Equal(OrderStatus.Ready.ToString(), item.Status);
    }

    [Fact]
    public async Task GetWorklistAsync_WithSearchText_FiltersByCustomerNamePhoneAndOrderId()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customerOne = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251900000001", "Harar");
        customerOne.StampCreated(clock.UtcNow, "owner-admin");
        customerOne.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customerOne);

        var customerTwo = new STailor.Core.Domain.Entities.CustomerProfile("Bilal Khan", "+251900000002", "Lahore");
        customerTwo.StampCreated(clock.UtcNow, "owner-admin");
        customerTwo.SetBaselineMeasurements("{\"Suit:Chest\":38}");
        await customerRepository.AddAsync(customerTwo);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var aminaOrder = await service.CreateOrderAsync(
            new CreateOrderCommand(customerOne.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(4)));
        _ = await service.CreateOrderAsync(
            new CreateOrderCommand(customerTwo.Id, "Suit", null, 1800m, 300m, clock.UtcNow.AddDays(5)));

        var byName = await service.GetWorklistAsync(includeDelivered: true, maxItems: 10, searchText: "amina");
        var byPhone = await service.GetWorklistAsync(includeDelivered: true, maxItems: 10, searchText: "000001");
        var byOrderId = await service.GetWorklistAsync(includeDelivered: true, maxItems: 10, searchText: aminaOrder.Id.ToString()[..8]);

        Assert.Single(byName);
        Assert.Single(byPhone);
        Assert.Single(byOrderId);
        Assert.Equal(aminaOrder.Id, byName[0].OrderId);
        Assert.Equal(aminaOrder.Id, byPhone[0].OrderId);
        Assert.Equal(aminaOrder.Id, byOrderId[0].OrderId);
    }

    [Fact]
    public async Task GetOrdersGroupedByStatusAsync_GroupsByWorkflowAndOrdersByCustomerName()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customerB = new STailor.Core.Domain.Entities.CustomerProfile("Samir Ali", "+251900000002", "Dire Dawa");
        customerB.StampCreated(clock.UtcNow, "owner-admin");
        customerB.SetBaselineMeasurements("{\"Suit:Chest\":38}");
        await customerRepository.AddAsync(customerB);

        var customerA = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251900000001", "Harar");
        customerA.StampCreated(clock.UtcNow, "owner-admin");
        customerA.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customerA);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        _ = await service.CreateOrderAsync(new CreateOrderCommand(customerB.Id, "Suit", null, 2500m, 500m, clock.UtcNow.AddDays(4)));
        var inProgressOrder = await service.CreateOrderAsync(new CreateOrderCommand(customerA.Id, "Suit", null, 2000m, 0m, clock.UtcNow.AddDays(2)));
        _ = await service.TransitionStatusAsync(new TransitionOrderStatusCommand(inProgressOrder.Id, OrderStatus.InProgress));

        var grouped = await service.GetOrdersGroupedByStatusAsync(includeDelivered: false, maxItems: 10);

        Assert.Equal(2, grouped.Count);
        Assert.Contains(OrderStatus.New, grouped.Keys);
        Assert.Contains(OrderStatus.InProgress, grouped.Keys);
        Assert.Equal("Samir Ali", grouped[OrderStatus.New][0].CustomerName);
        Assert.Equal("Amina Noor", grouped[OrderStatus.InProgress][0].CustomerName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOrder()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Delete Order", "+251944400000", "Jijiga");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var service = new OrderService(
            orderRepository,
            customerRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateOrderCommandValidator(),
            new AddPaymentCommandValidator(),
            new TransitionOrderStatusCommandValidator(),
            new ScheduleTrialFittingCommandValidator());

        var order = await service.CreateOrderAsync(
            new CreateOrderCommand(customer.Id, "Suit", null, 2200m, 300m, clock.UtcNow.AddDays(5)));

        await service.DeleteAsync(order.Id);

        var deleted = await orderRepository.GetByIdAsync(order.Id);
        Assert.Null(deleted);
    }

    private static SyncQueueService CreateSyncQueueService(FakeClock clock, string userId)
    {
        return new SyncQueueService(
            new InMemorySyncQueueRepository(),
            new FakeUnitOfWork(),
            new FakeCurrentUserService(userId),
            clock);
    }
}
