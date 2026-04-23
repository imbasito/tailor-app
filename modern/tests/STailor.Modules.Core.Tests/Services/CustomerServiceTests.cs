using STailor.Core.Application.Commands;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Tests.Fakes;
using STailor.Modules.Core.Validation;

namespace STailor.Modules.Core.Tests.Services;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesCustomerAndStampsAuditFields()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero));
        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var customer = await service.CreateAsync(
            new CreateCustomerCommand("Abdul Hakim", "+251900000000", "Harar", "VIP"));

        Assert.Equal("Abdul Hakim", customer.FullName);
        Assert.Equal("owner-admin", customer.CreatedBy);
        Assert.Equal("{}", customer.BaselineMeasurementsJson);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCustomerIdentityAndStampsAuditFields()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 13, 0, 0, TimeSpan.Zero));
        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Old Name", "+923001111111", "Lahore", "Old note");
        customer.StampCreated(clock.UtcNow.AddDays(-2), "owner-admin");
        await customerRepository.AddAsync(customer);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("front-desk"),
            clock,
            CreateSyncQueueService(clock, "front-desk"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var updated = await service.UpdateAsync(
            new UpdateCustomerCommand(customer.Id, "Noor Nawaz", "+923020000063", "Islamabad", "Prefers evening pickup"));

        Assert.Equal("Noor Nawaz", updated.FullName);
        Assert.Equal("+923020000063", updated.PhoneNumber);
        Assert.Equal("Islamabad", updated.City);
        Assert.Equal("Prefers evening pickup", updated.Notes);
        Assert.Equal("front-desk", updated.ModifiedBy);
        Assert.Equal(clock.UtcNow, updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpsertBaselineMeasurementsAsync_SavesNamespacedMeasurements()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 11, 0, 0, TimeSpan.Zero));
        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251911111111", "Dire Dawa");
        customer.StampCreated(new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero), "owner-admin");
        await customerRepository.AddAsync(customer);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("tailor-1"),
            clock,
            CreateSyncQueueService(clock, "tailor-1"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var updated = await service.UpsertBaselineMeasurementsAsync(
            new UpsertBaselineMeasurementsCommand(
                customer.Id,
                "Suit",
                new Dictionary<string, decimal>
                {
                    ["Chest"] = 40m,
                    ["Waist"] = 34m,
                }));

        Assert.Contains("Suit:Chest", updated.BaselineMeasurementsJson);
        Assert.Contains("Suit:Waist", updated.BaselineMeasurementsJson);
        Assert.Equal("tailor-1", updated.ModifiedBy);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCustomerWhenNoOrdersExist()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));
        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Delete Me", "+251922200000", "Harar");
        customer.StampCreated(clock.UtcNow.AddHours(-1), "owner-admin");
        await customerRepository.AddAsync(customer);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        await service.DeleteAsync(customer.Id);

        var deleted = await customerRepository.GetByIdAsync(customer.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetWorklistAsync_ReturnsCustomerSummariesWithOrderMetrics()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 15, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251911111111", "Dire Dawa", "VIP");
        customer.StampCreated(clock.UtcNow.AddDays(-10), "owner-admin");
        customer.StampUpdated(clock.UtcNow.AddDays(-1), "owner-admin");
        await customerRepository.AddAsync(customer);

        var orderOne = new STailor.Core.Domain.Entities.Order(
            customer.Id,
            "Suit",
            "{\"Chest\":40}",
            2500m,
            clock.UtcNow.AddDays(-3),
            clock.UtcNow.AddDays(2));
        orderOne.StampCreated(clock.UtcNow.AddDays(-3), "owner-admin");
        orderOne.ApplyPayment(500m, clock.UtcNow.AddDays(-3), "Deposit").StampCreated(clock.UtcNow.AddDays(-3), "owner-admin");
        await orderRepository.AddAsync(orderOne);

        var orderTwo = new STailor.Core.Domain.Entities.Order(
            customer.Id,
            "Shirt",
            "{\"Collar\":16}",
            1200m,
            clock.UtcNow.AddDays(-1),
            clock.UtcNow.AddDays(4));
        orderTwo.StampCreated(clock.UtcNow.AddDays(-1), "owner-admin");
        await orderRepository.AddAsync(orderTwo);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var items = await service.GetWorklistAsync("amina", 10);

        var item = Assert.Single(items);
        Assert.Equal(2, item.OrderCount);
        Assert.Equal(3200m, item.OutstandingBalance);
        Assert.Equal(orderTwo.ReceivedAtUtc, item.LastOrderReceivedAtUtc);
    }

    [Fact]
    public async Task GetWorkspaceDetailAsync_ReturnsBaselineAndRecentOrders()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 45, 0, TimeSpan.Zero));

        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Amina Noor", "+251911111111", "Dire Dawa", "Repeat customer");
        customer.StampCreated(clock.UtcNow.AddDays(-10), "owner-admin");
        customer.StampUpdated(clock.UtcNow.AddDays(-1), "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40,\"Shirt:Collar\":16}");
        await customerRepository.AddAsync(customer);

        var olderOrder = new STailor.Core.Domain.Entities.Order(
            customer.Id,
            "Suit",
            "{\"Chest\":40}",
            2500m,
            clock.UtcNow.AddDays(-5),
            clock.UtcNow.AddDays(2));
        olderOrder.StampCreated(clock.UtcNow.AddDays(-5), "owner-admin");
        await orderRepository.AddAsync(olderOrder);

        var newerOrder = new STailor.Core.Domain.Entities.Order(
            customer.Id,
            "Shirt",
            "{\"Collar\":16}",
            1200m,
            clock.UtcNow.AddDays(-1),
            clock.UtcNow.AddDays(4));
        newerOrder.StampCreated(clock.UtcNow.AddDays(-1), "owner-admin");
        newerOrder.ApplyPayment(200m, clock.UtcNow.AddDays(-1), "Deposit").StampCreated(clock.UtcNow.AddDays(-1), "owner-admin");
        await orderRepository.AddAsync(newerOrder);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var detail = await service.GetWorkspaceDetailAsync(customer.Id, 1);

        Assert.NotNull(detail);
        Assert.Contains("Suit:Chest", detail!.BaselineMeasurementsJson);
        Assert.Equal(3500m, detail.OutstandingBalance);
        var recentOrder = Assert.Single(detail.RecentOrders);
        Assert.Equal(newerOrder.Id, recentOrder.OrderId);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingOrders_Throws()
    {
        var customerRepository = new InMemoryCustomerProfileRepository();
        var orderRepository = new InMemoryOrderRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 18, 12, 30, 0, TimeSpan.Zero));
        var customer = new STailor.Core.Domain.Entities.CustomerProfile("Keep Me", "+251933300000", "Dire Dawa");
        customer.StampCreated(clock.UtcNow.AddHours(-1), "owner-admin");
        customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
        await customerRepository.AddAsync(customer);

        var existingOrder = new STailor.Core.Domain.Entities.Order(
            customer.Id,
            "Suit",
            "{\"Chest\":40}",
            2500m,
            clock.UtcNow.AddDays(-1),
            clock.UtcNow.AddDays(4));
        existingOrder.StampCreated(clock.UtcNow.AddHours(-1), "owner-admin");
        await orderRepository.AddAsync(existingOrder);

        var service = new CustomerService(
            customerRepository,
            orderRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserService("owner-admin"),
            clock,
            CreateSyncQueueService(clock, "owner-admin"),
            new MeasurementService(),
            new CreateCustomerCommandValidator(),
            new UpdateCustomerCommandValidator(),
            new UpsertBaselineMeasurementsCommandValidator());

        var exception = await Assert.ThrowsAsync<STailor.Core.Domain.Exceptions.DomainRuleViolationException>(
            () => service.DeleteAsync(customer.Id));

        Assert.Contains("cannot be deleted", exception.Message);
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
