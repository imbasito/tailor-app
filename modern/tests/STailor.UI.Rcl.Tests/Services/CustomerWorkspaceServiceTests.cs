using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Measurements;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class CustomerWorkspaceServiceTests
{
    [Fact]
    public async Task GetWorklistAsync_WithValidInputs_CallsCustomersEndpointAndReturnsItems()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson<IReadOnlyList<CustomerWorkspaceItemDto>>(HttpStatusCode.OK,
        [
            new CustomerWorkspaceItemDto(
                Guid.Parse("54832dd1-a94a-495d-a7d0-c9fb4d4b1061"),
                "Amina Noor",
                "+251900000001",
                "Addis Ababa",
                "VIP",
                2,
                900m,
                new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)),
        ]);

        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.GetWorklistAsync("http://localhost:5064", "amina", 25);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("Amina Noor", item.FullName);
        var requestUri = Assert.IsType<Uri>(handler.Requests.Single().RequestUri);
        Assert.Contains("search=amina", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("maxItems=25", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDetailAsync_WithValidInputs_CallsCustomerDetailEndpoint()
    {
        var handler = new StubHttpMessageHandler();
        var customerId = Guid.Parse("ce9f6dbf-bb1a-4e89-a45d-6f8e417a1ea6");
        handler.EnqueueJson(HttpStatusCode.OK,
            new CustomerWorkspaceDetailDto(
                customerId,
                "Amina Noor",
                "+251900000001",
                "Addis Ababa",
                null,
                "{\"Suit:Chest\":40}",
                900m,
                new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                []));

        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.GetDetailAsync("http://localhost:5064", customerId, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(customerId, result.Customer!.CustomerId);
        var requestUri = Assert.IsType<Uri>(handler.Requests.Single().RequestUri);
        Assert.EndsWith($"/api/customers/{customerId}", requestUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recentOrderLimit=5", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAsync_WhenApiReturnsBadRequest_ReturnsFailureMessage()
    {
        var handler = new StubHttpMessageHandler();
        var customerId = Guid.Parse("25261dba-6748-482f-a11f-5f4d0ea18874");
        handler.EnqueueRaw(HttpStatusCode.BadRequest, "{\"error\":\"Customer profile cannot be deleted while orders still exist.\"}");

        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.DeleteAsync("http://localhost:5064", customerId);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot be deleted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyCustomerId_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.DeleteAsync("http://localhost:5064", Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Contains("Customer id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateAsync_WithValidInput_CallsCustomerEndpoint()
    {
        var handler = new StubHttpMessageHandler();
        var customerId = Guid.Parse("144f6064-04d5-4eda-bf30-c2156cb0c56a");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            new CustomerProfileDto(
                customerId,
                "Noor Nawaz",
                "+923020000063",
                "Islamabad",
                "Prefers evening pickup",
                "{}",
                new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero)));

        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.UpdateAsync(
            "http://localhost:5064",
            customerId,
            "Noor Nawaz",
            "+923020000063",
            "Islamabad",
            "Prefers evening pickup");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Customer);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith($"/api/customers/{customerId}", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

        var payload = await request.Content!.ReadFromJsonAsync<UpdateCustomerRequest>();
        Assert.NotNull(payload);
        Assert.Equal("Noor Nawaz", payload!.FullName);
        Assert.Equal("+923020000063", payload.PhoneNumber);
    }

    [Fact]
    public async Task UpsertBaselineMeasurementsAsync_WithValidInput_CallsMeasurementEndpoint()
    {
        var handler = new StubHttpMessageHandler();
        var customerId = Guid.Parse("8fc1635b-aa24-411f-8b43-340fe734c5f0");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            new CustomerProfileDto(
                customerId,
                "Amina Noor",
                "+251900000001",
                "Addis Ababa",
                null,
                "{\"Suit:Chest\":40}",
                new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero)));

        var service = new CustomerWorkspaceService(new HttpClient(handler));
        var measurements = new Dictionary<string, decimal>
        {
            ["Chest"] = 40m,
            ["Waist"] = 32m,
        };

        var result = await service.UpsertBaselineMeasurementsAsync(
            "http://localhost:5064",
            customerId,
            "Suit",
            measurements);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Customer);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith($"/api/customers/{customerId}/measurements", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

        var payload = await request.Content!.ReadFromJsonAsync<MeasurementSetDto>();
        Assert.NotNull(payload);
        Assert.Equal("Suit", payload!.GarmentType);
        Assert.Equal(40m, payload.Measurements["Chest"]);
    }

    [Fact]
    public async Task UpsertBaselineMeasurementsAsync_WithNoMeasurements_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new CustomerWorkspaceService(new HttpClient(handler));

        var result = await service.UpsertBaselineMeasurementsAsync(
            "http://localhost:5064",
            Guid.Parse("fb7f2500-c7ee-488a-9f25-c67280bc3c53"),
            "Suit",
            new Dictionary<string, decimal>());

        Assert.False(result.IsSuccess);
        Assert.Contains("At least one measurement", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
