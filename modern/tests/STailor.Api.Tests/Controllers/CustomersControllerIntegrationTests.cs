using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Measurements;

namespace STailor.Api.Tests.Controllers;

public sealed class CustomersControllerIntegrationTests
{
    [Fact]
    public async Task GetCustomers_ReturnsWorkspaceItems()
    {
        await using var factory = new CustomersApiFactory();
        factory.FakeService.WorklistResult =
        [
            new CustomerWorkspaceItem(
                Guid.Parse("8d6e40a6-8641-4631-9496-f632825f368f"),
                "Amina Noor",
                "+251900000001",
                "Addis Ababa",
                "VIP",
                2,
                1200m,
                new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)),
        ];

        using var client = factory.CreateClient();

        var items = await client.GetFromJsonAsync<IReadOnlyList<CustomerWorkspaceItemDto>>("/api/customers?search=amina&maxItems=10");

        var item = Assert.Single(items!);
        Assert.Equal("Amina Noor", item.FullName);
        Assert.Equal("amina", factory.FakeService.LastSearchText);
        Assert.Equal(10, factory.FakeService.LastMaxItems);
    }

    [Fact]
    public async Task GetCustomer_WhenFound_ReturnsWorkspaceDetail()
    {
        await using var factory = new CustomersApiFactory();
        var customerId = Guid.Parse("74d15e85-22e6-47b1-b7df-5c6415ef73ea");
        factory.FakeService.DetailResult = new CustomerWorkspaceDetail(
            customerId,
            "Amina Noor",
            "+251900000001",
            "Addis Ababa",
            "Prefers Friday pickup",
            "{\"Suit:Chest\":40}",
            900m,
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
            [
                new CustomerWorkspaceOrder(
                    Guid.Parse("fd65d25b-9904-4a52-8c79-ad6fdc2456d0"),
                    "Suit",
                    "Ready",
                    2500m,
                    1600m,
                    900m,
                    new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
                    "{\"Chest\":40}")
            ]);

        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<CustomerWorkspaceDetailDto>($"/api/customers/{customerId}?recentOrderLimit=3");

        Assert.NotNull(detail);
        Assert.Equal(customerId, detail!.CustomerId);
        Assert.Single(detail.RecentOrders);
        Assert.Equal(3, factory.FakeService.LastRecentOrderLimit);
    }

    [Fact]
    public async Task DeleteCustomer_ReturnsNoContentAndCallsService()
    {
        await using var factory = new CustomersApiFactory();
        using var client = factory.CreateClient();

        var customerId = Guid.Parse("18fd2c8b-b2b1-4ff4-9106-e1f5239b93a8");
        using var response = await client.DeleteAsync($"/api/customers/{customerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(customerId, factory.FakeService.LastDeletedCustomerId);
    }

    [Fact]
    public async Task UpdateCustomer_ReturnsOkAndCallsService()
    {
        await using var factory = new CustomersApiFactory();
        using var client = factory.CreateClient();

        var customerId = Guid.Parse("6330ca23-3d59-4d93-a1be-0ea39b3aa7fa");
        using var response = await client.PutAsJsonAsync(
            $"/api/customers/{customerId}",
            new UpdateCustomerRequest(
                "Noor Nawaz",
                "+923020000063",
                "Islamabad",
                "Prefers evening pickup"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CustomerProfileDto>();
        Assert.NotNull(payload);
        Assert.Equal("Noor Nawaz", payload!.FullName);

        Assert.NotNull(factory.FakeService.LastUpdateCommand);
        Assert.Equal(customerId, factory.FakeService.LastUpdateCommand!.CustomerId);
        Assert.Equal("+923020000063", factory.FakeService.LastUpdateCommand.PhoneNumber);
    }

    [Fact]
    public async Task UpsertMeasurements_ReturnsOkAndCallsService()
    {
        await using var factory = new CustomersApiFactory();
        using var client = factory.CreateClient();

        var customerId = Guid.Parse("0bfeee90-eeb0-4f3f-ad95-fb9fb67f5bf5");
        using var response = await client.PutAsJsonAsync(
            $"/api/customers/{customerId}/measurements",
            new MeasurementSetDto(
                "Suit",
                new Dictionary<string, decimal>
                {
                    ["Chest"] = 40m,
                    ["Waist"] = 32m,
                }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CustomerProfileDto>();
        Assert.NotNull(payload);
        Assert.Contains("Suit:Chest", payload!.BaselineMeasurementsJson, StringComparison.Ordinal);

        Assert.NotNull(factory.FakeService.LastMeasurementCommand);
        Assert.Equal(customerId, factory.FakeService.LastMeasurementCommand!.CustomerId);
        Assert.Equal("Suit", factory.FakeService.LastMeasurementCommand.GarmentType);
        Assert.Equal(40m, factory.FakeService.LastMeasurementCommand.Measurements["Chest"]);
    }

    private sealed class CustomersApiFactory : WebApplicationFactory<Program>
    {
        public FakeCustomerService FakeService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICustomerService>();
                services.AddSingleton(FakeService);
                services.AddSingleton<ICustomerService>(provider =>
                    provider.GetRequiredService<FakeCustomerService>());
            });
        }
    }

    private sealed class FakeCustomerService : ICustomerService
    {
        public Guid? LastDeletedCustomerId { get; private set; }
        public UpsertBaselineMeasurementsCommand? LastMeasurementCommand { get; private set; }
        public UpdateCustomerCommand? LastUpdateCommand { get; private set; }
        public string? LastSearchText { get; private set; }
        public int? LastMaxItems { get; private set; }
        public int? LastRecentOrderLimit { get; private set; }
        public IReadOnlyList<CustomerWorkspaceItem> WorklistResult { get; set; } = [];
        public CustomerWorkspaceDetail? DetailResult { get; set; }

        public Task<CustomerProfile> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<CustomerProfile> UpdateAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default)
        {
            LastUpdateCommand = command;

            var customer = new CustomerProfile(command.FullName, command.PhoneNumber, command.City, command.Notes);
            customer.StampCreated(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), "owner-admin");
            customer.StampUpdated(new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero), "owner-admin");
            return Task.FromResult(customer);
        }

        public Task<IReadOnlyList<CustomerWorkspaceItem>> GetWorklistAsync(
            string? searchText,
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            LastSearchText = searchText;
            LastMaxItems = maxItems;
            return Task.FromResult(WorklistResult);
        }

        public Task<CustomerWorkspaceDetail?> GetWorkspaceDetailAsync(
            Guid customerId,
            int recentOrderLimit,
            CancellationToken cancellationToken = default)
        {
            LastRecentOrderLimit = recentOrderLimit;
            return Task.FromResult(DetailResult);
        }

        public Task<CustomerProfile> UpsertBaselineMeasurementsAsync(
            UpsertBaselineMeasurementsCommand command,
            CancellationToken cancellationToken = default)
        {
            LastMeasurementCommand = command;

            var customer = new CustomerProfile("Amina Noor", "+251900000001", "Addis Ababa");
            customer.StampCreated(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), "owner-admin");
            customer.SetBaselineMeasurements("{\"Suit:Chest\":40,\"Suit:Waist\":32}");
            customer.StampUpdated(new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero), "owner-admin");
            return Task.FromResult(customer);
        }

        public Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            LastDeletedCustomerId = customerId;
            return Task.CompletedTask;
        }
    }
}
