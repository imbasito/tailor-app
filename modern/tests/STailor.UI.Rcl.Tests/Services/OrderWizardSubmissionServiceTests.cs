using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Models;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrderWizardSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_WithNewStatus_SendsThreeRequestsAndReturnsSuccess()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, new CustomerProfileDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            null,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Suit",
            "New",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("New"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Amina Noor", result.CustomerName);
        Assert.Equal("+251900000001", result.PhoneNumber);
        Assert.Equal(2000m, result.BalanceDue);
        Assert.NotNull(result.DueAtUtc);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Collection(
            handler.Requests,
            first =>
            {
                Assert.Equal(HttpMethod.Post, first.Method);
                Assert.EndsWith("/api/customers", first.Uri, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Amina Noor", first.Body, StringComparison.Ordinal);
            },
            second =>
            {
                Assert.Equal(HttpMethod.Put, second.Method);
                Assert.Contains("/api/customers/11111111-1111-1111-1111-111111111111/measurements", second.Uri, StringComparison.OrdinalIgnoreCase);
            },
            third =>
            {
                Assert.Equal(HttpMethod.Post, third.Method);
                Assert.EndsWith("/api/orders", third.Uri, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("2500", third.Body, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task SubmitAsync_WithTransitionStatus_SendsSequentialStatusRequests()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, new CustomerProfileDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            null,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "New",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "InProgress",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "TrialFitting",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "Rework",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "Ready",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("Ready"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Ready", result.FinalStatus);
        Assert.Equal("Amina Noor", result.CustomerName);
        Assert.Equal("+251900000001", result.PhoneNumber);
        Assert.Equal(2000m, result.BalanceDue);
        Assert.Equal(7, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
        Assert.EndsWith("/api/orders/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/status", handler.Requests[3].Uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InProgress", handler.Requests[3].Body, StringComparison.Ordinal);
        Assert.Contains("TrialFitting", handler.Requests[4].Body, StringComparison.Ordinal);
        Assert.Contains("Rework", handler.Requests[5].Body, StringComparison.Ordinal);
        Assert.Contains("Ready", handler.Requests[6].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_WithSameStatus_DoesNotSendStatusRequests()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, new CustomerProfileDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            null,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "TrialFitting",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("TrialFitting"));

        Assert.True(result.IsSuccess);
        Assert.Equal("TrialFitting", result.FinalStatus);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SubmitAsync_WithBackwardStatus_ReturnsFailureWithoutStatusRequests()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, new CustomerProfileDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            null,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "Rework",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("InProgress"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot move order backwards", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SubmitAsync_FromIntermediateStatus_TransitionsStepwiseToTarget()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, new CustomerProfileDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Amina Noor",
            "+251900000001",
            "Harar",
            null,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "TrialFitting",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "Rework",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Suit",
            "Ready",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("Ready"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Ready", result.FinalStatus);
        Assert.Equal(5, handler.Requests.Count);
        Assert.Contains("Rework", handler.Requests[3].Body, StringComparison.Ordinal);
        Assert.Contains("Ready", handler.Requests[4].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_WhenCustomerCallFails_ReturnsFailureWithApiError()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(HttpStatusCode.BadRequest, "{\"error\":\"Phone already exists.\"}");

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("New"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Phone already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WithExistingCustomerId_SkipsCustomerCreationAndUsesExistingCustomer()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { ok = true });
        handler.EnqueueJson(HttpStatusCode.OK, new OrderDto(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Suit",
            "New",
            2500m,
            500m,
            2000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "{\"Chest\":40}"));

        var service = new OrderWizardSubmissionService(new HttpClient(handler));
        var result = await service.SubmitAsync(BuildRequest("New") with
        {
            ExistingCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.CustomerId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Collection(
            handler.Requests,
            first =>
            {
                Assert.Equal(HttpMethod.Put, first.Method);
                Assert.Contains("/api/customers/11111111-1111-1111-1111-111111111111/measurements", first.Uri, StringComparison.OrdinalIgnoreCase);
            },
            second =>
            {
                Assert.Equal(HttpMethod.Post, second.Method);
                Assert.EndsWith("/api/orders", second.Uri, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("11111111-1111-1111-1111-111111111111", second.Body, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static OrderWizardSubmissionRequest BuildRequest(string targetStatus)
    {
        return new OrderWizardSubmissionRequest(
            ApiBaseUrl: "http://localhost:5064",
            ExistingCustomerId: null,
            FullName: "Amina Noor",
            PhoneNumber: "+251900000001",
            City: "Harar",
            Notes: "VIP",
            GarmentType: "Suit",
            Measurements: new Dictionary<string, decimal>
            {
                ["Chest"] = 40m,
                ["Waist"] = 32m,
            },
            PhotoAttachments: Array.Empty<OrderWizardPhotoAttachmentInput>(),
            AmountCharged: 2500m,
            InitialDeposit: 500m,
            DueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
            TrialScheduledAtUtc: null,
            TrialScheduleStatus: "Scheduled",
            ApplyTrialStatusTransition: false,
            TargetStatus: targetStatus);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<CapturedRequest> Requests { get; } = new();

        public void EnqueueJson<T>(HttpStatusCode statusCode, T payload)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(payload),
            };

            _responses.Enqueue(response);
        }

        public void EnqueueRaw(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };

            _responses.Enqueue(response);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response available for request.");
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
