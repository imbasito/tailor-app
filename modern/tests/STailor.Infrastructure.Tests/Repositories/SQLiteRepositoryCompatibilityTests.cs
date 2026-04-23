using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Infrastructure.Persistence;
using STailor.Infrastructure.Repositories;

namespace STailor.Infrastructure.Tests.Repositories;

public sealed class SQLiteRepositoryCompatibilityTests
{
    [Fact]
    public async Task CustomerWorklistAsync_OrdersByUpdatedAtOnSqliteWithoutTranslationFailure()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var older = new CustomerProfile("Older Customer", "+251900000011", "Lahore");
        older.StampCreated(new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero), "seed");
        older.StampUpdated(new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero), "seed");

        var newer = new CustomerProfile("Newer Customer", "+251900000012", "Karachi");
        newer.StampCreated(new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero), "seed");
        newer.StampUpdated(new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero), "seed");

        await context.CustomerProfiles.AddRangeAsync(older, newer);
        await context.SaveChangesAsync();

        var repository = new EfCustomerProfileRepository(context);

        var result = await repository.GetWorklistAsync(null, 10);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
    }

    [Fact]
    public async Task OrderWorklistAsync_FiltersAndOrdersOnSqliteWithoutDateTimeOffsetTranslationFailure()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var customer = new CustomerProfile("Amina Noor", "+251900000021", "Islamabad");
        customer.StampCreated(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero), "seed");

        var dueSoon = new Order(
            customer.Id,
            "Suit",
            "{\"Chest\":40}",
            3000m,
            new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 24, 8, 0, 0, TimeSpan.Zero));
        dueSoon.StampCreated(new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero), "seed");

        var dueLater = new Order(
            customer.Id,
            "Shirt",
            "{\"Neck\":15}",
            1200m,
            new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 28, 8, 0, 0, TimeSpan.Zero));
        dueLater.TransitionTo(OrderStatus.InProgress);
        dueLater.StampCreated(new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero), "seed");

        await context.CustomerProfiles.AddAsync(customer);
        await context.Orders.AddRangeAsync(dueLater, dueSoon);
        await context.SaveChangesAsync();

        var repository = new EfOrderRepository(context);

        var result = await repository.GetWorklistAsync(
            includeDelivered: false,
            maxItems: 10,
            statusFilter: null,
            dueOnOrBeforeUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(result);
        Assert.Equal(dueSoon.Id, result[0].Id);
    }

    [Fact]
    public async Task ReminderCandidatesAsync_FiltersDueOrdersOnSqliteWithoutDateTimeOffsetTranslationFailure()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var customer = new CustomerProfile("Bilal Khan", "+251900000031", "Multan");
        customer.StampCreated(new DateTimeOffset(2026, 4, 18, 8, 0, 0, TimeSpan.Zero), "seed");

        var dueInsideWindow = new Order(
            customer.Id,
            "Trouser",
            "{\"Waist\":34}",
            900m,
            new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 8, 0, 0, TimeSpan.Zero));
        dueInsideWindow.StampCreated(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero), "seed");

        var dueOutsideWindow = new Order(
            customer.Id,
            "Coat",
            "{\"Chest\":42}",
            2500m,
            new DateTimeOffset(2026, 4, 19, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 30, 8, 0, 0, TimeSpan.Zero));
        dueOutsideWindow.StampCreated(new DateTimeOffset(2026, 4, 19, 9, 0, 0, TimeSpan.Zero), "seed");

        await context.CustomerProfiles.AddAsync(customer);
        await context.Orders.AddRangeAsync(dueInsideWindow, dueOutsideWindow);
        await context.SaveChangesAsync();

        var repository = new EfOrderRepository(context);

        var result = await repository.GetReminderCandidatesAsync(
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
            10);

        Assert.Single(result);
        Assert.Equal(dueInsideWindow.Id, result[0].Id);
    }

    [Fact]
    public async Task UpdateAsync_WithTrackedOrderAndNewPayment_PersistsPaymentWithoutConcurrencyError()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var customer = new CustomerProfile("Sana Malik", "+251900000041", "Lahore");
        customer.StampCreated(new DateTimeOffset(2026, 4, 18, 8, 0, 0, TimeSpan.Zero), "seed");

        var order = new Order(
            customer.Id,
            "Suit",
            "{\"Chest\":40}",
            2000m,
            new DateTimeOffset(2026, 4, 18, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 24, 8, 0, 0, TimeSpan.Zero));
        order.StampCreated(new DateTimeOffset(2026, 4, 18, 8, 0, 0, TimeSpan.Zero), "seed");

        await context.CustomerProfiles.AddAsync(customer);
        await context.Orders.AddAsync(order);
        await context.SaveChangesAsync();

        var repository = new EfOrderRepository(context);
        var trackedOrder = await repository.GetByIdAsync(order.Id);

        Assert.NotNull(trackedOrder);

        var payment = trackedOrder!.ApplyPayment(
            500m,
            new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero),
            "Deposit");
        payment.StampCreated(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero), "seed");
        trackedOrder.StampUpdated(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero), "seed");

        await repository.UpdateAsync(trackedOrder);
        await context.SaveChangesAsync();

        var persistedOrder = await repository.GetByIdAsync(order.Id);

        Assert.NotNull(persistedOrder);
        Assert.Equal(500m, persistedOrder!.AmountPaid);
        Assert.Single(persistedOrder.Payments);
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static LocalTailorDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<LocalTailorDbContext>()
            .UseSqlite(connection)
            .Options;

        return new LocalTailorDbContext(options);
    }
}
