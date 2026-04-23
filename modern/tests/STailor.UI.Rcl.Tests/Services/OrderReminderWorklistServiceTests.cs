using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrderReminderWorklistServiceTests
{
    [Fact]
    public async Task GetAsync_WithValidInputs_CallsRemindersEndpointAndReturnsItems()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson<IReadOnlyList<OrderReminderDto>>(HttpStatusCode.OK,
            [
                new OrderReminderDto(
                    OrderId: Guid.Parse("d0e37d3a-df2f-42fd-b2ab-7ddb96f59689"),
                    CustomerId: Guid.Parse("57a48df9-bd4d-4dab-9e3b-f8f79ea62107"),
                    CustomerName: "Amina Noor",
                    PhoneNumber: "+251900000001",
                    GarmentType: "Suit",
                    Status: "Ready",
                    AmountCharged: 2500m,
                    AmountPaid: 500m,
                    BalanceDue: 2000m,
                    DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero)),
            ]);

        var service = new OrderReminderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync("http://localhost:5064", daysAhead: 7, maxItems: 25);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("Amina Noor", item.CustomerName);
        Assert.Single(handler.Requests);
        var requestUri = Assert.IsType<Uri>(handler.Requests[0].RequestUri);
        Assert.EndsWith("/api/orders/reminders", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maxItems=25", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("dueOnOrBeforeUtc=", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithInvalidDaysAhead_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new OrderReminderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync("http://localhost:5064", daysAhead: -1, maxItems: 25);

        Assert.False(result.IsSuccess);
        Assert.Contains("Days ahead", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenApiReturnsError_ReturnsFailureWithApiMessage()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(HttpStatusCode.BadRequest, "{\"error\":\"Invalid reminder query.\"}");

        var service = new OrderReminderWorklistService(new HttpClient(handler));

        var result = await service.GetAsync("http://localhost:5064", daysAhead: 7, maxItems: 25);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid reminder query", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
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
