using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STailor.Core.Application.ReadModels;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Shared.Contracts.Orders;

namespace STailor.Api.Tests.Controllers;

public sealed class OrdersControllerIntegrationTests
{
    [Fact]
    public async Task GetOrder_WhenFound_ReturnsWorkspaceDetail()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("ddf37b78-f892-41af-929c-fdef3405efae");
        factory.FakeService.DetailResult = new OrderWorkspaceDetail(
            orderId,
            Guid.Parse("07c9b456-4ed4-4346-a6c6-870559ca4c54"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            "Suit",
            "Ready",
            3000m,
            750m,
            2250m,
            new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
            "{\"Chest\":40}",
            "[]",
            null,
            null,
            [
                new OrderPaymentHistoryItem(
                    Guid.Parse("7a929e4e-2fd1-4c5d-ac08-c52753fbbbe4"),
                    750m,
                    new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero),
                    "Balance pickup")
            ]);

        using var response = await client.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<OrderWorkspaceDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("Amina Noor", payload!.CustomerName);
        Assert.Single(payload.Payments);
    }

    [Fact]
    public async Task AddPayment_WithValidPayload_ReturnsOkAndCallsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("2e77fcb7-a0ce-442e-9935-b4f6a3fa4e2d");
        using var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/payments",
            new AddPaymentRequest(750m, new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero), "Balance pickup"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(payload);
        Assert.Equal(750m, payload!.AmountPaid);
        Assert.Equal(2250m, payload.BalanceDue);

        Assert.NotNull(factory.FakeService.LastPaymentCommand);
        Assert.Equal(orderId, factory.FakeService.LastPaymentCommand!.OrderId);
        Assert.Equal(750m, factory.FakeService.LastPaymentCommand.Amount);
    }

    [Fact]
    public async Task ScheduleTrial_WithValidPayload_ReturnsOk()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("950db13f-0918-411f-854e-22962dbd3271");
        using var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/schedule-trial",
            new ScheduleTrialFittingRequest(
                TrialAtUtc: new DateTimeOffset(2026, 5, 1, 14, 0, 0, TimeSpan.Zero),
                ScheduleStatus: "Scheduled",
                ApplyTrialStatusTransition: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(payload);
        Assert.Equal("TrialFitting", payload!.Status);
        Assert.Equal("Scheduled", payload.TrialScheduleStatus);
        Assert.NotNull(payload.TrialScheduledAtUtc);
    }

    [Fact]
    public async Task TransitionStatus_WithDefinedStatus_ReturnsOkAndCallsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("43f2e0a5-3952-4a71-a0df-6e0e55692e31");
        using var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/status",
            new TransitionOrderStatusRequest("InProgress"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(payload);
        Assert.Equal("InProgress", payload!.Status);

        Assert.NotNull(factory.FakeService.LastTransitionCommand);
        Assert.Equal(orderId, factory.FakeService.LastTransitionCommand!.OrderId);
        Assert.Equal(OrderStatus.InProgress, factory.FakeService.LastTransitionCommand.TargetStatus);
    }

    [Fact]
    public async Task TransitionStatus_WithAliasStatus_ReturnsOkAndCallsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("3f5de5a0-bf55-4f03-b6c2-fd010be669c9");
        using var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/status",
            new TransitionOrderStatusRequest("In Progress"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.FakeService.LastTransitionCommand);
        Assert.Equal(OrderStatus.InProgress, factory.FakeService.LastTransitionCommand!.TargetStatus);
    }

    [Fact]
    public async Task DeleteOrder_ReturnsNoContentAndCallsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var orderId = Guid.Parse("e4fdf393-627f-4511-a0ab-b8f7698d3668");
        using var response = await client.DeleteAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(orderId, factory.FakeService.LastDeletedOrderId);
    }

    [Fact]
    public async Task TransitionStatus_WithUndefinedNumericStatus_ReturnsBadRequestAndDoesNotCallService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/orders/d0f8dfbc-1814-4a37-85ca-8852f299f5cd/status",
            new TransitionOrderStatusRequest("999"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.FakeService.LastTransitionCommand);
    }

    [Fact]
    public async Task GetReminders_WithValidQuery_ReturnsReminderPayload()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var cutoff = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero);
        factory.FakeService.ReminderCandidates =
        [
            new OrderReminderCandidate(
                OrderId: Guid.Parse("63881d47-5877-469b-a50c-97393f6c5370"),
                CustomerId: Guid.Parse("84c7e9cd-e365-4f8d-a6f4-1e1dab4a47b1"),
                CustomerName: "Amina Noor",
                PhoneNumber: "+251900000001",
                GarmentType: "Suit",
                Status: "Ready",
                AmountCharged: 2500m,
                AmountPaid: 500m,
                BalanceDue: 2000m,
                DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero)),
        ];

        using var response = await client.GetAsync($"/api/orders/reminders?dueOnOrBeforeUtc={Uri.EscapeDataString(cutoff.ToString("O"))}&maxItems=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderReminderDto>>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!);
        Assert.Equal("Amina Noor", item.CustomerName);
        Assert.Equal(2000m, item.BalanceDue);

        Assert.Equal(cutoff, factory.FakeService.LastReminderDueOnOrBeforeUtc);
        Assert.Equal(5, factory.FakeService.LastReminderMaxItems);
    }

    [Fact]
    public async Task GetReminders_WithInvalidMaxItems_ReturnsBadRequestAndSkipsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/orders/reminders?maxItems=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.FakeService.LastReminderMaxItems);
    }

    [Fact]
    public async Task GetWorklist_WithValidQuery_ReturnsWorklistPayload()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        var dueCutoff = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero);

        factory.FakeService.WorklistItems =
        [
            new OrderWorklistItem(
                OrderId: Guid.Parse("9f757dc6-759f-4f40-883a-ea670529dad4"),
                CustomerId: Guid.Parse("dcdad972-f9a1-4642-b9ee-f31317ecad4d"),
                CustomerName: "Samir Ali",
                PhoneNumber: "+251900000002",
                City: "Dire Dawa",
                GarmentType: "Shirt",
                Status: "InProgress",
                AmountCharged: 1800m,
                AmountPaid: 400m,
                BalanceDue: 1400m,
                ReceivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                DueAtUtc: new DateTimeOffset(2026, 4, 26, 0, 0, 0, TimeSpan.Zero)),
        ];

        using var response = await client.GetAsync(
            $"/api/orders/worklist?includeDelivered=true&status=InProgress&overdueOnly=true&dueOnOrBeforeUtc={Uri.EscapeDataString(dueCutoff.ToString("O"))}&maxItems=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderWorklistItemDto>>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!);
        Assert.Equal("Samir Ali", item.CustomerName);
        Assert.Equal(1400m, item.BalanceDue);

        Assert.True(factory.FakeService.LastWorklistIncludeDelivered);
        Assert.Equal(OrderStatus.InProgress, factory.FakeService.LastWorklistStatusFilter);
        Assert.True(factory.FakeService.LastWorklistOverdueOnly);
        Assert.Equal(dueCutoff, factory.FakeService.LastWorklistDueOnOrBeforeUtc);
        Assert.Equal(7, factory.FakeService.LastWorklistMaxItems);
    }

    [Fact]
    public async Task GetWorklist_WithAliasStatus_ReturnsWorklistPayload()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        factory.FakeService.WorklistItems =
        [
            new OrderWorklistItem(
                OrderId: Guid.Parse("8f95d95b-a4c5-4986-9e0f-8cd20d8e5e20"),
                CustomerId: Guid.Parse("6bd5f158-8d3f-42d7-9c6f-0a2d95dc0771"),
                CustomerName: "Amina Noor",
                PhoneNumber: "+251900000001",
                City: "Harar",
                GarmentType: "Suit",
                Status: "TrialFitting",
                AmountCharged: 2500m,
                AmountPaid: 500m,
                BalanceDue: 2000m,
                ReceivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                DueAtUtc: new DateTimeOffset(2026, 4, 26, 0, 0, 0, TimeSpan.Zero)),
        ];

        using var response = await client.GetAsync("/api/orders/worklist?status=Trial/Fitting&maxItems=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.TrialFitting, factory.FakeService.LastWorklistStatusFilter);
    }

    [Fact]
    public async Task GetWorklist_WithSearchQuery_PassesSearchToService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        factory.FakeService.WorklistItems = [];

        using var response = await client.GetAsync("/api/orders/worklist?search=amina&maxItems=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("amina", factory.FakeService.LastWorklistSearchText);
    }

    [Fact]
    public async Task GetWorklist_WithAnyStatusAlias_DoesNotApplyStatusFilter()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        factory.FakeService.WorklistItems = [];

        using var response = await client.GetAsync("/api/orders/worklist?status=all&maxItems=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(factory.FakeService.LastWorklistStatusFilter);
    }

    [Fact]
    public async Task GetWorklist_WithInvalidMaxItems_ReturnsBadRequestAndSkipsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/orders/worklist?maxItems=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.FakeService.LastWorklistMaxItems);
    }

    [Fact]
    public async Task GetWorklist_WithInvalidStatus_ReturnsBadRequestAndSkipsService()
    {
        await using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/orders/worklist?status=invalid-status");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.FakeService.LastWorklistMaxItems);
    }

    private sealed class OrdersApiFactory : WebApplicationFactory<Program>
    {
        public FakeOrderService FakeService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOrderService>();
                services.AddSingleton(FakeService);
                services.AddSingleton<IOrderService>(provider =>
                    provider.GetRequiredService<FakeOrderService>());
            });
        }
    }

    private sealed class FakeOrderService : IOrderService
    {
        public OrderWorkspaceDetail? DetailResult { get; set; }

        public AddPaymentCommand? LastPaymentCommand { get; private set; }

        public TransitionOrderStatusCommand? LastTransitionCommand { get; private set; }

        public Guid? LastDeletedOrderId { get; private set; }

        public DateTimeOffset? LastReminderDueOnOrBeforeUtc { get; private set; }

        public int? LastReminderMaxItems { get; private set; }

        public bool LastWorklistIncludeDelivered { get; private set; }

        public OrderStatus? LastWorklistStatusFilter { get; private set; }

        public bool LastWorklistOverdueOnly { get; private set; }

        public DateTimeOffset? LastWorklistDueOnOrBeforeUtc { get; private set; }

        public string? LastWorklistSearchText { get; private set; }

        public int? LastWorklistMaxItems { get; private set; }

        public IReadOnlyList<OrderReminderCandidate> ReminderCandidates { get; set; } = [];

        public IReadOnlyList<OrderWorklistItem> WorklistItems { get; set; } = [];

        public Task<Order> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<Order> AddPaymentAsync(AddPaymentCommand command, CancellationToken cancellationToken = default)
        {
            LastPaymentCommand = command;

            var order = CreateOrderWithStatus(OrderStatus.Ready);
            var payment = order.ApplyPayment(command.Amount, command.PaidAtUtc ?? new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero), command.Note);
            payment.StampCreated(new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero), "owner-admin");
            return Task.FromResult(order);
        }

        public Task<Order> TransitionStatusAsync(
            TransitionOrderStatusCommand command,
            CancellationToken cancellationToken = default)
        {
            LastTransitionCommand = command;
            return Task.FromResult(CreateOrderWithStatus(command.TargetStatus));
        }

        public Task<Order> ScheduleTrialFittingAsync(
            ScheduleTrialFittingCommand command,
            CancellationToken cancellationToken = default)
        {
            var order = CreateOrderWithStatus(OrderStatus.InProgress);
            order.ScheduleTrial(command.TrialAtUtc, command.ScheduleStatus);
            if (command.ApplyTrialStatusTransition)
            {
                order.TransitionTo(OrderStatus.TrialFitting);
            }

            return Task.FromResult(order);
        }

        public Task<OrderWorkspaceDetail?> GetWorkspaceDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DetailResult);
        }

        public Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            LastDeletedOrderId = orderId;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OrderReminderCandidate>> GetReminderCandidatesAsync(
            DateTimeOffset dueOnOrBeforeUtc,
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            LastReminderDueOnOrBeforeUtc = dueOnOrBeforeUtc;
            LastReminderMaxItems = maxItems;
            return Task.FromResult(ReminderCandidates);
        }

        public Task<IReadOnlyList<OrderWorklistItem>> GetWorklistAsync(
            bool includeDelivered,
            int maxItems,
            OrderStatus? statusFilter = null,
            bool overdueOnly = false,
            DateTimeOffset? dueOnOrBeforeUtc = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            LastWorklistIncludeDelivered = includeDelivered;
            LastWorklistMaxItems = maxItems;
            LastWorklistStatusFilter = statusFilter;
            LastWorklistOverdueOnly = overdueOnly;
            LastWorklistDueOnOrBeforeUtc = dueOnOrBeforeUtc;
            LastWorklistSearchText = searchText;
            return Task.FromResult(WorklistItems);
        }

        public Task<IDictionary<OrderStatus, List<OrderWorklistItem>>> GetOrdersGroupedByStatusAsync(
            bool includeDelivered,
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDictionary<OrderStatus, List<OrderWorklistItem>>>(
                new Dictionary<OrderStatus, List<OrderWorklistItem>>());
        }

        private static Order CreateOrderWithStatus(OrderStatus status)
        {
            var order = new Order(
                customerProfileId: Guid.Parse("2779b5cb-ee6c-4763-8ebd-f6a0f07b11ee"),
                garmentType: "Suit",
                measurementSnapshotJson: "{\"Chest\":40}",
                amountCharged: 3000m,
                receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

            if (status == OrderStatus.New)
            {
                return order;
            }

            var progression = new[]
            {
                OrderStatus.InProgress,
                OrderStatus.TrialFitting,
                OrderStatus.Rework,
                OrderStatus.Ready,
                OrderStatus.Delivered,
            };

            foreach (var step in progression)
            {
                order.TransitionTo(step);
                if (step == status)
                {
                    break;
                }
            }

            return order;
        }
    }
}
