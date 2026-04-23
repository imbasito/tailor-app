using STailor.Core.Application.Migration;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Tests.Fakes;

namespace STailor.Modules.Core.Tests.Services;

public sealed class LegacyMigrationServiceTests
{
    [Fact]
    public async Task ImportAsync_DefaultFilters_ImportsActiveCustomersAndOpenOrders()
    {
        var mapper = new FakeLegacyMigrationMapper();
        var customerService = new FakeCustomerService();
        var orderService = new FakeOrderService();
        var service = new LegacyMigrationService(mapper, customerService, orderService);

        var batch = new LegacyMigrationBatch(
            Customers:
            [
                new LegacyCustomerRecord(1, "Amina", "+2519001", "Harar", null, IsActive: true),
                new LegacyCustomerRecord(2, "Samir", "+2519002", "Dire Dawa", null, IsActive: false),
                new LegacyCustomerRecord(3, "Nur", "+2519003", "Jigjiga", null, IsActive: true),
            ],
            Orders:
            [
                new LegacyOrderRecord(100, 1, "Suit", "2026-04-01", "100", "20", "2026-04-08", IsOpen: true),
                new LegacyOrderRecord(101, 1, "Suit", "2026-04-01", "200", "50", "2026-04-08", IsOpen: false),
                new LegacyOrderRecord(102, 2, "Shirt", "2026-04-01", "80", "10", "2026-04-08", IsOpen: true),
                new LegacyOrderRecord(103, 3, "Trouser", "2026-04-01", "60", "0", "2026-04-08", IsOpen: true),
            ]);

        var report = await service.ImportAsync(batch);

        Assert.Equal(3, report.InputCustomerCount);
        Assert.Equal(4, report.InputOrderCount);
        Assert.Equal(2, report.FilteredCustomerCount);
        Assert.Equal(3, report.FilteredOrderCount);
        Assert.Equal(1, report.SkippedInactiveCustomerCount);
        Assert.Equal(1, report.SkippedClosedOrderCount);
        Assert.Equal(2, report.ImportedCustomerCount);
        Assert.Equal(2, report.ImportedOrderCount);
        Assert.Equal(160m, report.SourceChargedTotal);
        Assert.Equal(20m, report.SourcePaidTotal);
        Assert.Equal(160m, report.ImportedChargedTotal);
        Assert.Equal(20m, report.ImportedPaidTotal);
        Assert.Equal(140m, report.ImportedBalanceTotal);
        Assert.Contains(report.Issues, issue => issue.LegacyId == 102 && issue.Message.Contains("Referenced customer id 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WhenMappingFails_ContinuesAndRecordsIssues()
    {
        var mapper = new FakeLegacyMigrationMapper();
        mapper.CustomerIdsToFail.Add(1);
        mapper.OrderIdsToFail.Add(202);

        var customerService = new FakeCustomerService();
        var orderService = new FakeOrderService();
        var service = new LegacyMigrationService(mapper, customerService, orderService);

        var batch = new LegacyMigrationBatch(
            Customers:
            [
                new LegacyCustomerRecord(1, "Bad Customer", "+2519100", "Harar", null, IsActive: true),
                new LegacyCustomerRecord(2, "Good Customer", "+2519200", "Harar", null, IsActive: true),
            ],
            Orders:
            [
                new LegacyOrderRecord(201, 1, "Suit", "2026-04-01", "90", "10", "2026-04-10", IsOpen: true),
                new LegacyOrderRecord(202, 2, "Shirt", "2026-04-01", "70", "10", "2026-04-10", IsOpen: true),
                new LegacyOrderRecord(203, 2, "Trouser", "2026-04-01", "50", "0", "2026-04-10", IsOpen: true),
            ]);

        var report = await service.ImportAsync(batch);

        Assert.Equal(1, report.ImportedCustomerCount);
        Assert.Equal(1, report.ImportedOrderCount);
        Assert.Equal(50m, report.SourceChargedTotal);
        Assert.Equal(0m, report.SourcePaidTotal);
        Assert.Equal(50m, report.ImportedChargedTotal);
        Assert.Equal(0m, report.ImportedPaidTotal);
        Assert.True(report.Issues.Count >= 2);
        Assert.Contains(report.Issues, issue => issue.EntityType == nameof(LegacyCustomerRecord) && issue.LegacyId == 1);
        Assert.Contains(report.Issues, issue => issue.EntityType == nameof(LegacyOrderRecord) && issue.LegacyId == 202);
    }

    [Fact]
    public async Task ImportAsync_WhenImportedTotalsDiffer_AddsParityIssue()
    {
        var mapper = new FakeLegacyMigrationMapper();
        var customerService = new FakeCustomerService();
        var orderService = new FakeOrderService
        {
            IgnoreInitialDeposit = true,
        };

        var service = new LegacyMigrationService(mapper, customerService, orderService);

        var batch = new LegacyMigrationBatch(
            Customers:
            [
                new LegacyCustomerRecord(7, "Parity", "+2519333", "Harar", null, IsActive: true),
            ],
            Orders:
            [
                new LegacyOrderRecord(301, 7, "Suit", "2026-04-01", "100", "30", "2026-04-10", IsOpen: true),
            ]);

        var report = await service.ImportAsync(batch);

        Assert.Equal(100m, report.SourceChargedTotal);
        Assert.Equal(30m, report.SourcePaidTotal);
        Assert.Equal(100m, report.ImportedChargedTotal);
        Assert.Equal(0m, report.ImportedPaidTotal);
        Assert.Contains(report.Issues, issue => issue.EntityType == "Parity" && issue.Message.Contains("Paid totals mismatch", StringComparison.OrdinalIgnoreCase));
    }
}
