using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Migration;
using STailor.Shared.Contracts.Migration;

namespace STailor.Api.Tests.Controllers;

public sealed class MigrationControllerIntegrationTests
{
    [Fact]
    public async Task Import_WithValidPayload_ReturnsMappedReportAndCallsService()
    {
        await using var factory = new MigrationApiFactory();
        factory.FakeService.Report = new LegacyMigrationReport(
            InputCustomerCount: 2,
            InputOrderCount: 1,
            FilteredCustomerCount: 2,
            FilteredOrderCount: 1,
            ImportedCustomerCount: 2,
            ImportedOrderCount: 1,
            SkippedInactiveCustomerCount: 0,
            SkippedClosedOrderCount: 0,
            SourceChargedTotal: 120m,
            SourcePaidTotal: 20m,
            ImportedChargedTotal: 120m,
            ImportedPaidTotal: 20m,
            ImportedBalanceTotal: 100m,
            Issues:
            [
                new LegacyMigrationIssue("LegacyOrderRecord", 77, "Missing mapped customer."),
            ]);

        using var client = factory.CreateClient();

        var payload = new LegacyMigrationImportRequest(
            Customers:
            [
                new LegacyCustomerMigrationDto(1, "Amina", "+2519001", "Harar", "VIP", IsActive: true),
                new LegacyCustomerMigrationDto(2, "Samir", "+2519002", "Dire Dawa", null, IsActive: false),
            ],
            Orders:
            [
                new LegacyOrderMigrationDto(77, 2, "Suit", "2026-04-10", "120", "20", "2026-04-18", IsOpen: true),
            ],
            ImportInactiveCustomers: true,
            ImportClosedOrders: true);

        using var response = await client.PostAsJsonAsync("/api/migration/import", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<LegacyMigrationReportDto>();
        Assert.NotNull(report);
        Assert.Equal(2, report!.InputCustomerCount);
        Assert.Equal(1, report.InputOrderCount);
        Assert.Equal(120m, report.SourceChargedTotal);
        Assert.Equal(100m, report.ImportedBalanceTotal);
        Assert.Single(report.Issues);
        Assert.Equal("LegacyOrderRecord", report.Issues[0].EntityType);
        Assert.Equal(77, report.Issues[0].LegacyId);

        Assert.NotNull(factory.FakeService.LastBatch);
        Assert.True(factory.FakeService.LastBatch!.ImportInactiveCustomers);
        Assert.True(factory.FakeService.LastBatch.ImportClosedOrders);
        Assert.Equal(2, factory.FakeService.LastBatch.Customers.Count);
        Assert.Single(factory.FakeService.LastBatch.Orders);
        Assert.Equal("Amina", factory.FakeService.LastBatch.Customers[0].FullName);
        Assert.Equal(77, factory.FakeService.LastBatch.Orders[0].LegacyId);
    }

    [Fact]
    public async Task Import_WithOmittedCollections_ReturnsBadRequest()
    {
        await using var factory = new MigrationApiFactory();
        factory.FakeService.Report = new LegacyMigrationReport(
            InputCustomerCount: 0,
            InputOrderCount: 0,
            FilteredCustomerCount: 0,
            FilteredOrderCount: 0,
            ImportedCustomerCount: 0,
            ImportedOrderCount: 0,
            SkippedInactiveCustomerCount: 0,
            SkippedClosedOrderCount: 0,
            SourceChargedTotal: 0m,
            SourcePaidTotal: 0m,
            ImportedChargedTotal: 0m,
            ImportedPaidTotal: 0m,
            ImportedBalanceTotal: 0m,
            Issues: []);

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/migration/import", new { importInactiveCustomers = false, importClosedOrders = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.FakeService.LastBatch);
    }

    private sealed class MigrationApiFactory : WebApplicationFactory<Program>
    {
        public FakeLegacyMigrationService FakeService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILegacyMigrationService>();
                services.AddSingleton(FakeService);
                services.AddSingleton<ILegacyMigrationService>(provider =>
                    provider.GetRequiredService<FakeLegacyMigrationService>());
            });
        }
    }

    private sealed class FakeLegacyMigrationService : ILegacyMigrationService
    {
        public LegacyMigrationBatch? LastBatch { get; private set; }

        public LegacyMigrationReport Report { get; set; } = new(
            InputCustomerCount: 0,
            InputOrderCount: 0,
            FilteredCustomerCount: 0,
            FilteredOrderCount: 0,
            ImportedCustomerCount: 0,
            ImportedOrderCount: 0,
            SkippedInactiveCustomerCount: 0,
            SkippedClosedOrderCount: 0,
            SourceChargedTotal: 0m,
            SourcePaidTotal: 0m,
            ImportedChargedTotal: 0m,
            ImportedPaidTotal: 0m,
            ImportedBalanceTotal: 0m,
            Issues: []);

        public Task<LegacyMigrationReport> ImportAsync(
            LegacyMigrationBatch batch,
            CancellationToken cancellationToken = default)
        {
            LastBatch = batch;
            return Task.FromResult(Report);
        }
    }
}
