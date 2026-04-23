using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Tests.Persistence;

public sealed class DevelopmentSampleDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_AddsDemoCustomersOrdersAndPayments()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        await DevelopmentSampleDataSeeder.SeedAsync(context);

        Assert.Equal(DevelopmentSampleDataSeeder.DemoCustomerCount, await context.CustomerProfiles.CountAsync());
        Assert.Equal(DevelopmentSampleDataSeeder.DemoCustomerCount, await context.Orders.CountAsync());
        Assert.True(await context.Payments.CountAsync() >= 700);
        Assert.Equal(
            DevelopmentSampleDataSeeder.DemoCustomerCount,
            await context.CustomerProfiles.CountAsync(customer => customer.PhoneNumber.StartsWith("+92")));
    }

    [Fact]
    public async Task SeedAsync_WhenCalledTwice_DoesNotDuplicateRecords()
    {
        await using var connection = CreateConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        await DevelopmentSampleDataSeeder.SeedAsync(context);
        var customerCount = await context.CustomerProfiles.CountAsync();
        var orderCount = await context.Orders.CountAsync();
        var paymentCount = await context.Payments.CountAsync();

        await DevelopmentSampleDataSeeder.SeedAsync(context);

        Assert.Equal(customerCount, await context.CustomerProfiles.CountAsync());
        Assert.Equal(orderCount, await context.Orders.CountAsync());
        Assert.Equal(paymentCount, await context.Payments.CountAsync());
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
