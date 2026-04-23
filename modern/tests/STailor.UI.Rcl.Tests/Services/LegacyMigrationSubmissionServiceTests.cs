using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using STailor.Shared.Contracts.Migration;
using STailor.UI.Rcl.Models;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class LegacyMigrationSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_WithValidPayload_PostsImportRequestAndReturnsReport()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new LegacyMigrationReportDto(
            InputCustomerCount: 2,
            InputOrderCount: 2,
            FilteredCustomerCount: 2,
            FilteredOrderCount: 2,
            ImportedCustomerCount: 2,
            ImportedOrderCount: 2,
            SkippedInactiveCustomerCount: 0,
            SkippedClosedOrderCount: 0,
            SourceChargedTotal: 200m,
            SourcePaidTotal: 30m,
            ImportedChargedTotal: 200m,
            ImportedPaidTotal: 30m,
            ImportedBalanceTotal: 170m,
            Issues: []));

        var service = new LegacyMigrationSubmissionService(new HttpClient(handler));

        var result = await service.SubmitAsync(new LegacyMigrationSubmissionRequest(
            ApiBaseUrl: "http://localhost:5064",
            CustomersJson:
            """
            [
              { "legacyId": 1, "fullName": "Amina", "phone": "+2519001", "city": "Harar", "comment": null, "isActive": true }
            ]
            """,
            OrdersJson:
            """
            [
              { "legacyId": 10, "legacyCustomerId": 1, "description": "Suit", "recievedOn": "2026-04-10", "amountCharged": "200", "amountPaid": "30", "collectingOn": "2026-04-18", "isOpen": true }
            ]
            """,
            ImportInactiveCustomers: true,
            ImportClosedOrders: false));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Report);
        Assert.Equal(200m, result.Report!.SourceChargedTotal);
        Assert.Single(handler.Requests);
        Assert.EndsWith("/api/migration/import", handler.Requests[0].Uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"importInactiveCustomers\":true", handler.Requests[0].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"legacyId\":1", handler.Requests[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidCustomersJson_ReturnsFailureWithoutRequest()
    {
        var handler = new StubHttpMessageHandler();
        var service = new LegacyMigrationSubmissionService(new HttpClient(handler));

        var result = await service.SubmitAsync(new LegacyMigrationSubmissionRequest(
            ApiBaseUrl: "http://localhost:5064",
            CustomersJson: "[{ invalid-json }]",
            OrdersJson: "[]",
            ImportInactiveCustomers: false,
            ImportClosedOrders: false));

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid customers JSON", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WhenApiReturnsError_ReturnsFailureWithApiMessage()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(HttpStatusCode.BadRequest, "{\"error\":\"Migration payload failed validation.\"}");

        var service = new LegacyMigrationSubmissionService(new HttpClient(handler));

        var result = await service.SubmitAsync(new LegacyMigrationSubmissionRequest(
            ApiBaseUrl: "http://localhost:5064",
            CustomersJson: "[]",
            OrdersJson: "[]",
            ImportInactiveCustomers: false,
            ImportClosedOrders: false));

        Assert.False(result.IsSuccess);
        Assert.Contains("failed validation", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<CapturedRequest> Requests { get; } = [];

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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response is available for this request.");
            }

            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body));

            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body);
}
