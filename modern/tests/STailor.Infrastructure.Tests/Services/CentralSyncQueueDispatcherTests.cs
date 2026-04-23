using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;
using STailor.Infrastructure.Persistence;
using STailor.Infrastructure.Services;

namespace STailor.Infrastructure.Tests.Services;

public sealed class CentralSyncQueueDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithOrderUpsert_ReplicatesOrderAndDependencies()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var nowUtc = new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Abdul Hakim", "+251911111111", "Addis Ababa", "VIP");
            customer.SetBaselineMeasurements("{\"chest\":42}");
            customer.StampCreated(nowUtc, "seed");

            var order = new Order(
                customer.Id,
                "Suit",
                "{\"waist\":36}",
                amountCharged: 2500m,
                receivedAtUtc: nowUtc.AddDays(-2),
                dueAtUtc: nowUtc.AddDays(5));

            order.TransitionTo(OrderStatus.InProgress);
            order.TransitionTo(OrderStatus.TrialFitting);

            var payment = order.ApplyPayment(500m, nowUtc.AddDays(-1), "Deposit");
            payment.StampCreated(nowUtc.AddDays(-1), "seed");
            order.StampCreated(nowUtc.AddDays(-2), "seed");
            order.StampUpdated(nowUtc.AddDays(-1), "seed");

            await localContext.CustomerProfiles.AddAsync(customer);
            await localContext.Orders.AddAsync(order);
            await localContext.SaveChangesAsync();

            var queueItem = new SyncQueueItem(
                entityType: "Order",
                entityId: order.Id,
                operation: "upsert",
                payloadJson: "{}",
                entityUpdatedAtUtc: order.UpdatedAtUtc,
                enqueuedAtUtc: nowUtc);

            queueItem.StampCreated(nowUtc, "sync-worker");

            var dispatcher = CreateDispatcher(localContext, centralContext);

            await dispatcher.DispatchAsync(queueItem);

            var centralCustomer = await centralContext.CustomerProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == customer.Id);

            var centralOrder = await centralContext.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == order.Id);

            var centralPayment = await centralContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            Assert.NotNull(centralCustomer);
            Assert.NotNull(centralOrder);
            Assert.NotNull(centralPayment);

            Assert.Equal(customer.FullName, centralCustomer!.FullName);
            Assert.Equal(order.Status, centralOrder!.Status);
            Assert.Equal(order.AmountPaid, centralOrder.AmountPaid);
            Assert.Equal(payment.OrderId, centralPayment!.OrderId);
            Assert.Equal(payment.Amount, centralPayment.Amount);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task DispatchAsync_WithPaymentDelete_RemovesCentralPayment()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var nowUtc = new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Muna Ali", "+251922222222", "Bahir Dar");
            customer.StampCreated(nowUtc.AddDays(-3), "seed");

            var order = new Order(
                customer.Id,
                "Dress",
                "{\"length\":120}",
                amountCharged: 1200m,
                receivedAtUtc: nowUtc.AddDays(-3),
                dueAtUtc: nowUtc.AddDays(2));

            var payment = order.ApplyPayment(400m, nowUtc.AddDays(-2), "Deposit");
            payment.StampCreated(nowUtc.AddDays(-2), "seed");
            order.StampCreated(nowUtc.AddDays(-3), "seed");
            order.StampUpdated(nowUtc.AddDays(-2), "seed");

            await localContext.CustomerProfiles.AddAsync(customer);
            await localContext.Orders.AddAsync(order);
            await localContext.SaveChangesAsync();

            var dispatcher = CreateDispatcher(localContext, centralContext);

            var upsertQueueItem = new SyncQueueItem(
                entityType: "Order",
                entityId: order.Id,
                operation: "upsert",
                payloadJson: "{}",
                entityUpdatedAtUtc: order.UpdatedAtUtc,
                enqueuedAtUtc: nowUtc);

            upsertQueueItem.StampCreated(nowUtc, "sync-worker");
            await dispatcher.DispatchAsync(upsertQueueItem);

            var deleteQueueItem = new SyncQueueItem(
                entityType: "Payment",
                entityId: payment.Id,
                operation: "delete",
                payloadJson: "{}",
                entityUpdatedAtUtc: nowUtc,
                enqueuedAtUtc: nowUtc.AddMinutes(1));

            deleteQueueItem.StampCreated(nowUtc.AddMinutes(1), "sync-worker");
            await dispatcher.DispatchAsync(deleteQueueItem);

            var centralPayment = await centralContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            var tombstone = await centralContext.SyncDeletionTombstones
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.EntityType == "payment" && item.EntityId == payment.Id);

            var centralOrder = await centralContext.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == order.Id);

            Assert.Null(centralPayment);
            Assert.NotNull(tombstone);
            Assert.Equal(nowUtc, tombstone!.DeletedAtUtc);
            Assert.NotNull(centralOrder);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task DispatchAsync_WithUnsupportedEntityType_Throws()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var queueItem = new SyncQueueItem(
                entityType: "UnknownEntity",
                entityId: Guid.NewGuid(),
                operation: "upsert",
                payloadJson: "{}",
                entityUpdatedAtUtc: DateTimeOffset.UtcNow,
                enqueuedAtUtc: DateTimeOffset.UtcNow);

            queueItem.StampCreated(DateTimeOffset.UtcNow, "sync-worker");

            var dispatcher = CreateDispatcher(localContext, centralContext);

            await Assert.ThrowsAsync<DomainRuleViolationException>(() => dispatcher.DispatchAsync(queueItem));
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task DispatchAsync_WithOlderLocalOrderUpsert_DoesNotOverwriteNewerCentralState()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);
            var localUpdatedAtUtc = baseUtc.AddMinutes(-10);
            var centralUpdatedAtUtc = baseUtc.AddMinutes(5);

            var localCustomer = new CustomerProfile("Local Customer", "+251933333333", "Adama", "Older local");
            localCustomer.SetBaselineMeasurements("{\"chest\":40}");
            localCustomer.StampCreated(baseUtc.AddDays(-2), "local-seed");
            localCustomer.StampUpdated(localUpdatedAtUtc, "local-seed");

            var localOrder = new Order(
                localCustomer.Id,
                "Suit",
                "{\"waist\":34}",
                amountCharged: 1800m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(3));

            localOrder.TransitionTo(OrderStatus.InProgress);
            localOrder.StampCreated(baseUtc.AddDays(-2), "local-seed");
            localOrder.StampUpdated(localUpdatedAtUtc, "local-seed");

            await localContext.CustomerProfiles.AddAsync(localCustomer);
            await localContext.Orders.AddAsync(localOrder);
            await localContext.SaveChangesAsync();

            var centralCustomer = new CustomerProfile("Central Customer", "+251944444444", "Addis Ababa", "Newer central");
            centralCustomer.SetBaselineMeasurements("{\"chest\":44}");
            centralCustomer.StampCreated(baseUtc.AddDays(-2), "central-seed");
            centralCustomer.StampUpdated(centralUpdatedAtUtc, "central-seed");

            var centralCustomerEntry = await centralContext.CustomerProfiles.AddAsync(centralCustomer);
            centralCustomerEntry.Property(nameof(CustomerProfile.Id)).CurrentValue = localCustomer.Id;

            var centralOrder = new Order(
                localCustomer.Id,
                "Suit",
                "{\"waist\":38}",
                amountCharged: 2600m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(4));

            centralOrder.TransitionTo(OrderStatus.InProgress);
            centralOrder.TransitionTo(OrderStatus.TrialFitting);
            centralOrder.TransitionTo(OrderStatus.Rework);
            centralOrder.TransitionTo(OrderStatus.Ready);
            centralOrder.StampCreated(baseUtc.AddDays(-2), "central-seed");
            centralOrder.StampUpdated(centralUpdatedAtUtc, "central-seed");

            var centralOrderEntry = await centralContext.Orders.AddAsync(centralOrder);
            centralOrderEntry.Property(nameof(Order.Id)).CurrentValue = localOrder.Id;
            await centralContext.SaveChangesAsync();

            var queueItem = new SyncQueueItem(
                entityType: "Order",
                entityId: localOrder.Id,
                operation: "upsert",
                payloadJson: "{}",
                entityUpdatedAtUtc: localOrder.UpdatedAtUtc,
                enqueuedAtUtc: baseUtc.AddMinutes(6));

            queueItem.StampCreated(baseUtc.AddMinutes(6), "sync-worker");

            var dispatcher = CreateDispatcher(localContext, centralContext);
            await dispatcher.DispatchAsync(queueItem);

            var persistedCustomer = await centralContext.CustomerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.Id == localCustomer.Id);

            var persistedOrder = await centralContext.Orders
                .AsNoTracking()
                .SingleAsync(item => item.Id == localOrder.Id);

            Assert.Equal("Central Customer", persistedCustomer.FullName);
            Assert.Equal("+251944444444", persistedCustomer.PhoneNumber);
            Assert.Equal(OrderStatus.Ready, persistedOrder.Status);
            Assert.Equal(2600m, persistedOrder.AmountCharged);
            Assert.Equal(centralUpdatedAtUtc, persistedOrder.UpdatedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task DispatchAsync_WithOrderUpsert_PreservesCentralOnlyPayments()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 13, 0, 0, TimeSpan.Zero);

            var localCustomer = new CustomerProfile("Aster Omar", "+251955555555", "Dire Dawa");
            localCustomer.StampCreated(baseUtc.AddDays(-2), "local-seed");
            localCustomer.StampUpdated(baseUtc.AddMinutes(5), "local-seed");

            var localOrder = new Order(
                localCustomer.Id,
                "Dress",
                "{\"length\":118}",
                amountCharged: 2100m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(2));

            localOrder.TransitionTo(OrderStatus.InProgress);
            localOrder.StampCreated(baseUtc.AddDays(-2), "local-seed");
            localOrder.StampUpdated(baseUtc.AddMinutes(5), "local-seed");

            await localContext.CustomerProfiles.AddAsync(localCustomer);
            await localContext.Orders.AddAsync(localOrder);
            await localContext.SaveChangesAsync();

            var centralCustomer = new CustomerProfile("Aster Omar", "+251955555555", "Dire Dawa");
            centralCustomer.StampCreated(baseUtc.AddDays(-2), "central-seed");
            centralCustomer.StampUpdated(baseUtc.AddMinutes(-10), "central-seed");
            var centralCustomerEntry = await centralContext.CustomerProfiles.AddAsync(centralCustomer);
            centralCustomerEntry.Property(nameof(CustomerProfile.Id)).CurrentValue = localCustomer.Id;

            var centralOrder = new Order(
                localCustomer.Id,
                "Dress",
                "{\"length\":110}",
                amountCharged: 1900m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(2));

            centralOrder.StampCreated(baseUtc.AddDays(-2), "central-seed");
            centralOrder.StampUpdated(baseUtc.AddMinutes(-10), "central-seed");
            var centralOrderEntry = await centralContext.Orders.AddAsync(centralOrder);
            centralOrderEntry.Property(nameof(Order.Id)).CurrentValue = localOrder.Id;

            var centralOnlyPayment = centralOrder.ApplyPayment(300m, baseUtc.AddDays(-1), "Central only");
            centralOnlyPayment.StampCreated(baseUtc.AddDays(-1), "central-seed");

            await centralContext.SaveChangesAsync();

            var queueItem = new SyncQueueItem(
                entityType: "Order",
                entityId: localOrder.Id,
                operation: "upsert",
                payloadJson: "{}",
                entityUpdatedAtUtc: localOrder.UpdatedAtUtc,
                enqueuedAtUtc: baseUtc.AddMinutes(6));

            queueItem.StampCreated(baseUtc.AddMinutes(6), "sync-worker");

            var dispatcher = CreateDispatcher(localContext, centralContext);
            await dispatcher.DispatchAsync(queueItem);

            var persistedCentralOnlyPayment = await centralContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == centralOnlyPayment.Id);

            var persistedOrder = await centralContext.Orders
                .AsNoTracking()
                .SingleAsync(item => item.Id == localOrder.Id);

            Assert.NotNull(persistedCentralOnlyPayment);
            Assert.Equal(300m, persistedCentralOnlyPayment!.Amount);
            Assert.Equal(OrderStatus.InProgress, persistedOrder.Status);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task DispatchAsync_WithOlderPaymentDelete_DoesNotRemoveNewerCentralPayment()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 14, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Selam Yusuf", "+251966666666", "Harar");
            customer.StampCreated(baseUtc.AddDays(-3), "central-seed");

            var order = new Order(
                customer.Id,
                "Shalwar Kameez",
                "{\"inseam\":40}",
                amountCharged: 1500m,
                receivedAtUtc: baseUtc.AddDays(-3),
                dueAtUtc: baseUtc.AddDays(1));

            var payment = order.ApplyPayment(700m, baseUtc.AddDays(-2), "Central deposit");
            payment.StampCreated(baseUtc.AddDays(-2), "central-seed");
            payment.StampUpdated(baseUtc.AddMinutes(10), "central-seed");
            order.StampCreated(baseUtc.AddDays(-3), "central-seed");
            order.StampUpdated(baseUtc.AddMinutes(10), "central-seed");

            await centralContext.CustomerProfiles.AddAsync(customer);
            await centralContext.Orders.AddAsync(order);
            await centralContext.SaveChangesAsync();

            var queueItem = new SyncQueueItem(
                entityType: "Payment",
                entityId: payment.Id,
                operation: "delete",
                payloadJson: "{}",
                entityUpdatedAtUtc: baseUtc.AddMinutes(5),
                enqueuedAtUtc: baseUtc.AddMinutes(6));

            queueItem.StampCreated(baseUtc.AddMinutes(6), "sync-worker");

            var dispatcher = CreateDispatcher(localContext, centralContext);
            await dispatcher.DispatchAsync(queueItem);

            var persistedPayment = await centralContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            Assert.NotNull(persistedPayment);
            Assert.Equal(700m, persistedPayment!.Amount);
            Assert.Equal(baseUtc.AddMinutes(10), persistedPayment.UpdatedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    private static LocalTailorDbContext CreateLocalContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<LocalTailorDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new LocalTailorDbContext(options);
    }

    private static CentralTailorDbContext CreateCentralContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<CentralTailorDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new CentralTailorDbContext(options);
    }

    private static CentralSyncQueueDispatcher CreateDispatcher(
        LocalTailorDbContext localContext,
        CentralTailorDbContext centralContext)
    {
        return new CentralSyncQueueDispatcher(localContext, centralContext, new TestSyncConflictResolver());
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"stailor-sync-{Guid.NewGuid():N}.db");
    }

    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Ignore transient file locks in temp cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore transient file locks in temp cleanup.
        }
    }

    private sealed class TestSyncConflictResolver : ISyncConflictResolver
    {
        public bool ShouldApplyRemote(DateTimeOffset localUpdatedAtUtc, DateTimeOffset remoteUpdatedAtUtc)
        {
            return remoteUpdatedAtUtc > localUpdatedAtUtc;
        }
    }
}
