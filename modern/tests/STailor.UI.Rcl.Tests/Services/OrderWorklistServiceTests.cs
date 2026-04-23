using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrderWorklistServiceTests
{
    [Fact]
    public async Task GetAsync_WithValidInputs_CallsWorklistEndpointAndReturnsItems()
    {
        var handler = new StubHttpMessageHandler();
        var dueCutoff = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero);
        handler.EnqueueJson<IReadOnlyList<OrderWorklistItemDto>>(HttpStatusCode.OK,
            [
                new OrderWorklistItemDto(
                    OrderId: Guid.Parse("3caceeb3-feb6-4bcb-80f9-244bbd6f91bf"),
                    CustomerId: Guid.Parse("cb9b902d-a97b-47b3-bf7a-8ee5700c4f7f"),
                    CustomerName: "Amina Noor",
                    PhoneNumber: "+251900000001",
                    City: "Addis Ababa",
                    GarmentType: "Suit",
                    Status: "InProgress",
                    AmountCharged: 2500m,
                    AmountPaid: 500m,
                    BalanceDue: 2000m,
                    ReceivedAtUtc: new DateTimeOffset(2026, 4, 19, 0, 0, 0, TimeSpan.Zero),
                    DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero)),
            ]);

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync(
            "http://localhost:5064",
            includeDelivered: true,
            maxItems: 25,
            statusFilter: "InProgress",
            overdueOnly: true,
            dueOnOrBeforeUtc: dueCutoff);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("Amina Noor", item.CustomerName);
        Assert.Single(handler.Requests);

        var requestUri = Assert.IsType<Uri>(handler.Requests[0].RequestUri);
        Assert.EndsWith("/api/orders/worklist", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("includeDelivered=true", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("status=InProgress", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("overdueOnly=true", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("dueOnOrBeforeUtc=", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("maxItems=25", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithSearchText_AddsSearchQuery()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson<IReadOnlyList<OrderWorklistItemDto>>(HttpStatusCode.OK, []);

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync(
            "http://localhost:5064",
            includeDelivered: true,
            maxItems: 10,
            searchText: "amina");

        Assert.True(result.IsSuccess);
        var requestUri = Assert.IsType<Uri>(handler.Requests.Single().RequestUri);
        Assert.Contains("search=amina", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithInvalidMaxItems_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync("http://localhost:5064", includeDelivered: false, maxItems: 0);

        Assert.False(result.IsSuccess);
        Assert.Contains("Max items", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenApiReturnsError_ReturnsFailureWithApiMessage()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(HttpStatusCode.BadRequest, "{\"error\":\"Invalid worklist query.\"}");

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync("http://localhost:5064", includeDelivered: false, maxItems: 25);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid worklist query", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithValidInputs_PostsEndpointAndReturnsOrder()
    {
        var handler = new StubHttpMessageHandler();
        var orderId = Guid.Parse("6f2ef8df-682d-46d4-8018-cb06b6e9e5e1");
        handler.EnqueueJson(HttpStatusCode.OK,
            new OrderDto(
                Id: orderId,
                CustomerId: Guid.Parse("ca4d69a5-ebf0-49a1-8807-afd9be43842d"),
                GarmentType: "Suit",
                Status: "InProgress",
                AmountCharged: 2500m,
                AmountPaid: 500m,
                BalanceDue: 2000m,
                ReceivedAtUtc: new DateTimeOffset(2026, 4, 19, 0, 0, 0, TimeSpan.Zero),
                DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
                MeasurementSnapshotJson: "{\"Chest\":40}"));

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.TransitionStatusAsync(
            "http://localhost:5064",
            orderId,
            targetStatus: "InProgress");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal("InProgress", result.Order!.Status);
        Assert.Single(handler.Requests);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        var requestUri = Assert.IsType<Uri>(request.RequestUri);
        Assert.EndsWith($"/api/orders/{orderId}/status", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithBlankStatus_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.TransitionStatusAsync(
            "http://localhost:5064",
            Guid.Parse("8e89b50c-a2aa-417f-b735-1a4fb9d63564"),
            targetStatus: " ");

        Assert.False(result.IsSuccess);
        Assert.Contains("Target status", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetDetailAsync_WithValidInputs_CallsDetailEndpointAndReturnsOrder()
    {
        var handler = new StubHttpMessageHandler();
        var orderId = Guid.Parse("ddf37b78-f892-41af-929c-fdef3405efae");
        handler.EnqueueJson(HttpStatusCode.OK,
            new OrderWorkspaceDetailDto(
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
                    new OrderPaymentHistoryItemDto(
                        Guid.Parse("7a929e4e-2fd1-4c5d-ac08-c52753fbbbe4"),
                        750m,
                        new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero),
                        "Balance pickup")
                ]));

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.GetDetailAsync("http://localhost:5064", orderId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal("Amina Noor", result.Order!.CustomerName);
        Assert.Single(result.Order.Payments);
        var requestUri = Assert.IsType<Uri>(handler.Requests.Single().RequestUri);
        Assert.EndsWith($"/api/orders/{orderId}", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_WithValidInputs_DeletesEndpointAndReturnsSuccess()
    {
        var handler = new StubHttpMessageHandler();
        var orderId = Guid.Parse("3f185b38-e4b6-4868-9208-f83bb0fd7c4f");
        handler.EnqueueRaw(HttpStatusCode.NoContent, string.Empty);

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.DeleteAsync("http://localhost:5064", orderId);

        Assert.True(result.IsSuccess);
        Assert.Single(handler.Requests);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Delete, request.Method);
        var requestUri = Assert.IsType<Uri>(request.RequestUri);
        Assert.EndsWith($"/api/orders/{orderId}", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyOrderId_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.DeleteAsync("http://localhost:5064", Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Contains("Order id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AddPaymentAsync_WithValidInputs_PostsPaymentEndpointAndReturnsOrder()
    {
        var handler = new StubHttpMessageHandler();
        var orderId = Guid.Parse("90dbab07-60f7-47d8-a05d-fbff84d2cd2e");
        handler.EnqueueJson(HttpStatusCode.OK,
            new OrderDto(
                Id: orderId,
                CustomerId: Guid.Parse("ca4d69a5-ebf0-49a1-8807-afd9be43842d"),
                GarmentType: "Suit",
                Status: "Ready",
                AmountCharged: 2500m,
                AmountPaid: 1500m,
                BalanceDue: 1000m,
                ReceivedAtUtc: new DateTimeOffset(2026, 4, 19, 0, 0, 0, TimeSpan.Zero),
                DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
                MeasurementSnapshotJson: "{\"Chest\":40}"));

        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.AddPaymentAsync(
            "http://localhost:5064",
            orderId,
            amount: 1000m,
            note: "Balance collected");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal(1000m, result.Order!.BalanceDue);
        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        var requestUri = Assert.IsType<Uri>(request.RequestUri);
        Assert.EndsWith($"/api/orders/{orderId}/payments", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddPaymentAsync_WithInvalidAmount_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new OrderWorklistService(new HttpClient(handler));

        var result = await service.AddPaymentAsync(
            "http://localhost:5064",
            Guid.Parse("90dbab07-60f7-47d8-a05d-fbff84d2cd2e"),
            amount: 0m,
            note: null);

        Assert.False(result.IsSuccess);
        Assert.Contains("greater than zero", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void EnqueueJson<T>(HttpStatusCode statusCode, T payload)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(payload),
            });
        }

        public void EnqueueRaw(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response available for request.");
            }

            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
