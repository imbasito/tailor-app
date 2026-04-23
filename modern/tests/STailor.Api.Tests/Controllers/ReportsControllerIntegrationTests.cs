using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;
using STailor.Shared.Contracts.Reports;

namespace STailor.Api.Tests.Controllers;

public sealed class ReportsControllerIntegrationTests
{
    [Fact]
    public async Task GetOperationsReport_ReturnsReportAndPassesFilters()
    {
        await using var factory = new ReportsApiFactory();
        factory.FakeService.OperationsReport = new OperationsReport
        {
            GeneratedAt = new DateTime(2026, 4, 23, 10, 0, 0),
            TotalOrders = 1,
            OpenOrders = 1,
            TotalCharged = 3200m,
            TotalPaid = 1200m,
            TotalBalanceDue = 2000m,
            Orders =
            [
                new OperationsReportOrder
                {
                    OrderId = Guid.Parse("8efc4c86-0000-0000-0000-000000000001"),
                    CustomerId = Guid.Parse("f183a3ef-0000-0000-0000-000000000001"),
                    CustomerName = "Usman Akhtar",
                    CustomerPhone = "+923130000224",
                    City = "Quetta",
                    GarmentType = "Trouser",
                    Status = "Ready",
                    AmountCharged = 3200m,
                    AmountPaid = 1200m,
                    BalanceDue = 2000m,
                    ReceivedAt = new DateTime(2026, 3, 9),
                    DueAt = new DateTime(2026, 3, 11),
                    IsOverdue = true,
                    DaysLate = 43,
                }
            ],
        };

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/reports/operations?search=usman&status=Ready&includeDelivered=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<OperationsReportDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalOrders);
        Assert.Equal("Usman Akhtar", Assert.Single(payload.Orders).CustomerName);
        Assert.NotNull(factory.FakeService.LastOperationsFilter);
        Assert.Equal("usman", factory.FakeService.LastOperationsFilter!.SearchText);
        Assert.Equal("Ready", factory.FakeService.LastOperationsFilter.Status);
        Assert.False(factory.FakeService.LastOperationsFilter.IncludeDelivered);
    }

    private sealed class ReportsApiFactory : WebApplicationFactory<Program>
    {
        public FakeReportingService FakeService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReportingService>();
                services.AddSingleton(FakeService);
                services.AddSingleton<IReportingService>(provider =>
                    provider.GetRequiredService<FakeReportingService>());
            });
        }
    }

    private sealed class FakeReportingService : IReportingService
    {
        public OperationsReport OperationsReport { get; set; } = new();

        public OperationsReportFilter? LastOperationsFilter { get; private set; }

        public Task<OperationsReport> GetOperationsReportAsync(
            OperationsReportFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastOperationsFilter = filter;
            return Task.FromResult(OperationsReport);
        }

        public Task<DailyOrdersReport> GetDailyOrdersReportAsync(DateTime? date = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<OutstandingDuesReport> GetOutstandingDuesReportAsync(OutstandingDuesFilter? filter = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<CustomerMeasurementHistoryReport> GetCustomerMeasurementHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<DeliveryQueueReport> GetDeliveryQueueAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }
    }
}
