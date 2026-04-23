using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Infrastructure.Persistence;
using STailor.Infrastructure.Services;

namespace STailor.Infrastructure.Tests.Services;

public sealed class CentralSyncPullServiceTests
{
    [Fact]
    public async Task PullAsync_PullsCentralCustomerOrderAndPaymentIntoLocalStore()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var nowUtc = new DateTimeOffset(2026, 4, 21, 15, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Fatima Noor", "+251977777777", "Lahore", "Remote profile");
            customer.SetBaselineMeasurements("{\"Suit:Chest\":40}");
            customer.StampCreated(nowUtc.AddMinutes(-5), "central-seed");
            customer.StampUpdated(nowUtc.AddMinutes(-5), "central-seed");

            var order = new Order(
                customer.Id,
                "Suit",
                "{\"Chest\":40}",
                amountCharged: 3200m,
                receivedAtUtc: nowUtc.AddDays(-1),
                dueAtUtc: nowUtc.AddDays(4));

            order.TransitionTo(OrderStatus.InProgress);
            order.StampCreated(nowUtc.AddMinutes(-4), "central-seed");
            order.StampUpdated(nowUtc.AddMinutes(-4), "central-seed");

            var payment = order.ApplyPayment(1200m, nowUtc.AddMinutes(-3), "Advance");
            payment.StampCreated(nowUtc.AddMinutes(-3), "central-seed");
            payment.StampUpdated(nowUtc.AddMinutes(-3), "central-seed");
            order.StampUpdated(nowUtc.AddMinutes(-3), "central-seed");

            await centralContext.CustomerProfiles.AddAsync(customer);
            await centralContext.Orders.AddAsync(order);
            await centralContext.SaveChangesAsync();

            var service = CreateService(localContext, centralContext);
            var result = await service.PullAsync(25);

            var localCustomer = await localContext.CustomerProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == customer.Id);

            var localOrder = await localContext.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == order.Id);

            var localPayment = await localContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            var cursors = await localContext.SyncPullCursors
                .AsNoTracking()
                .OrderBy(item => item.Scope)
                .ToListAsync();

            Assert.NotNull(localCustomer);
            Assert.NotNull(localOrder);
            Assert.NotNull(localPayment);
            Assert.Equal(1, result.CustomersApplied);
            Assert.Equal(1, result.OrdersApplied);
            Assert.Equal(1, result.PaymentsApplied);
            Assert.Equal(3, cursors.Count);
            Assert.Contains(cursors, item => item.Scope == "customer_profiles" && item.LastSyncedAtUtc == customer.UpdatedAtUtc);
            Assert.Contains(cursors, item => item.Scope == "orders" && item.LastSyncedAtUtc == order.UpdatedAtUtc);
            Assert.Contains(cursors, item => item.Scope == "payments" && item.LastSyncedAtUtc == payment.UpdatedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task PullAsync_DoesNotOverwriteNewerLocalCustomer()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 16, 0, 0, TimeSpan.Zero);

            var centralCustomer = new CustomerProfile("Older Central", "+251988888888", "Karachi");
            centralCustomer.SetBaselineMeasurements("{\"Suit:Chest\":39}");
            centralCustomer.StampCreated(baseUtc.AddDays(-1), "central-seed");
            centralCustomer.StampUpdated(baseUtc.AddMinutes(-10), "central-seed");

            var localCustomer = new CustomerProfile("Newer Local", "+251999999999", "Islamabad");
            localCustomer.SetBaselineMeasurements("{\"Suit:Chest\":41}");
            localCustomer.StampCreated(baseUtc.AddDays(-1), "local-seed");
            localCustomer.StampUpdated(baseUtc.AddMinutes(5), "local-seed");

            var centralEntry = await centralContext.CustomerProfiles.AddAsync(centralCustomer);
            centralEntry.Property(nameof(CustomerProfile.Id)).CurrentValue = localCustomer.Id;
            await localContext.CustomerProfiles.AddAsync(localCustomer);

            await centralContext.SaveChangesAsync();
            await localContext.SaveChangesAsync();

            var service = CreateService(localContext, centralContext);
            var result = await service.PullAsync(25);

            var persistedLocalCustomer = await localContext.CustomerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.Id == localCustomer.Id);

            var customerCursor = await localContext.SyncPullCursors
                .AsNoTracking()
                .SingleAsync(item => item.Scope == "customer_profiles");

            Assert.Equal("Newer Local", persistedLocalCustomer.FullName);
            Assert.Equal("+251999999999", persistedLocalCustomer.PhoneNumber);
            Assert.Equal(0, result.CustomersApplied);
            Assert.Equal(centralCustomer.UpdatedAtUtc, customerCursor.LastSyncedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task PullAsync_WithSharedBoundaryTimestamp_PullsEntireTimestampGroup()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var sharedUpdatedAtUtc = new DateTimeOffset(2026, 4, 21, 17, 0, 0, TimeSpan.Zero);

            var customerOne = new CustomerProfile("Customer One", "+251900000001", "Faisalabad");
            customerOne.StampCreated(sharedUpdatedAtUtc.AddMinutes(-1), "central-seed");
            customerOne.StampUpdated(sharedUpdatedAtUtc, "central-seed");

            var customerTwo = new CustomerProfile("Customer Two", "+251900000002", "Multan");
            customerTwo.StampCreated(sharedUpdatedAtUtc.AddMinutes(-1), "central-seed");
            customerTwo.StampUpdated(sharedUpdatedAtUtc, "central-seed");

            await centralContext.CustomerProfiles.AddRangeAsync(customerOne, customerTwo);
            await centralContext.SaveChangesAsync();

            var service = CreateService(localContext, centralContext);
            var firstResult = await service.PullAsync(1);
            var secondResult = await service.PullAsync(1);

            var localCustomers = await localContext.CustomerProfiles
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .ToListAsync();

            Assert.Equal(2, firstResult.CustomersProcessed);
            Assert.Equal(2, firstResult.CustomersApplied);
            Assert.Empty(localContext.ChangeTracker.Entries().Where(entry => entry.State != EntityState.Unchanged));
            Assert.Equal(0, secondResult.TotalProcessed);
            Assert.Equal(2, localCustomers.Count);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task PullAsync_AppliesCentralOrderDeletionTombstoneToLocalStore()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 18, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Delete Flow", "+251911000000", "Peshawar");
            customer.StampCreated(baseUtc.AddDays(-2), "seed");
            customer.StampUpdated(baseUtc.AddDays(-2), "seed");

            var order = new Order(
                customer.Id,
                "Suit",
                "{\"Chest\":42}",
                amountCharged: 2800m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(3));

            order.StampCreated(baseUtc.AddDays(-2), "seed");
            order.StampUpdated(baseUtc.AddMinutes(-10), "seed");

            var payment = order.ApplyPayment(800m, baseUtc.AddDays(-1), "Deposit");
            payment.StampCreated(baseUtc.AddDays(-1), "seed");
            payment.StampUpdated(baseUtc.AddMinutes(-10), "seed");

            await localContext.CustomerProfiles.AddAsync(customer);
            await localContext.Orders.AddAsync(order);
            await localContext.SaveChangesAsync();

            var tombstone = new SyncDeletionTombstone("order", order.Id, baseUtc);
            await centralContext.SyncDeletionTombstones.AddAsync(tombstone);
            await centralContext.SaveChangesAsync();

            var service = CreateService(localContext, centralContext);
            var result = await service.PullAsync(25);

            var localOrder = await localContext.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == order.Id);

            var localPayment = await localContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            var tombstoneCursor = await localContext.SyncPullCursors
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == "deletion_tombstones");

            Assert.Equal(0, result.TotalApplied);
            Assert.Null(localOrder);
            Assert.Null(localPayment);
            Assert.NotNull(tombstoneCursor);
            Assert.Equal(baseUtc, tombstoneCursor!.LastSyncedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    [Fact]
    public async Task PullAsync_DoesNotApplyOlderCentralPaymentDeletionTombstone()
    {
        var localDbPath = CreateTempDbPath();
        var centralDbPath = CreateTempDbPath();

        try
        {
            await using var localContext = CreateLocalContext(localDbPath);
            await using var centralContext = CreateCentralContext(centralDbPath);

            await localContext.Database.EnsureCreatedAsync();
            await centralContext.Database.EnsureCreatedAsync();

            var baseUtc = new DateTimeOffset(2026, 4, 21, 19, 0, 0, TimeSpan.Zero);

            var customer = new CustomerProfile("Keep Local", "+251922000000", "Rawalpindi");
            customer.StampCreated(baseUtc.AddDays(-2), "seed");
            customer.StampUpdated(baseUtc.AddDays(-2), "seed");

            var order = new Order(
                customer.Id,
                "Dress",
                "{\"Length\":55}",
                amountCharged: 2100m,
                receivedAtUtc: baseUtc.AddDays(-2),
                dueAtUtc: baseUtc.AddDays(2));

            order.StampCreated(baseUtc.AddDays(-2), "seed");
            order.StampUpdated(baseUtc.AddMinutes(5), "seed");

            var payment = order.ApplyPayment(500m, baseUtc.AddDays(-1), "Latest local");
            payment.StampCreated(baseUtc.AddDays(-1), "seed");
            payment.StampUpdated(baseUtc.AddMinutes(5), "seed");

            await localContext.CustomerProfiles.AddAsync(customer);
            await localContext.Orders.AddAsync(order);
            await localContext.SaveChangesAsync();

            var tombstone = new SyncDeletionTombstone("payment", payment.Id, baseUtc.AddMinutes(-5));
            await centralContext.SyncDeletionTombstones.AddAsync(tombstone);
            await centralContext.SaveChangesAsync();

            var service = CreateService(localContext, centralContext);
            await service.PullAsync(25);

            var localPayment = await localContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == payment.Id);

            Assert.NotNull(localPayment);
            Assert.Equal(500m, localPayment!.Amount);
            Assert.Equal(baseUtc.AddMinutes(5), localPayment.UpdatedAtUtc);
        }
        finally
        {
            DeleteIfExists(localDbPath);
            DeleteIfExists(centralDbPath);
        }
    }

    private static CentralSyncPullService CreateService(
        LocalTailorDbContext localContext,
        CentralTailorDbContext centralContext)
    {
        return new CentralSyncPullService(localContext, centralContext, new TestSyncConflictResolver());
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

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"stailor-pull-{Guid.NewGuid():N}.db");
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
