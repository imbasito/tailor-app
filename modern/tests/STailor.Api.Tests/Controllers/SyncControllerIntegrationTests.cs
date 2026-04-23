using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Shared.Contracts.Sync;

namespace STailor.Api.Tests.Controllers;

public sealed class SyncControllerIntegrationTests
{
    [Fact]
    public async Task GetDiagnostics_ReturnsPayloadFromService()
    {
        await using var factory = new SyncApiFactory();
        factory.FakeService.Diagnostics = new SyncQueueDiagnostics(
            PendingCount: 4,
            FailedCount: 2,
            SyncedCount: 7,
            RetryDueCount: 1,
            TotalCount: 13,
            OldestPendingEnqueuedAtUtc: new DateTimeOffset(2026, 4, 20, 9, 30, 0, TimeSpan.Zero),
            EvaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 10, 15, 0, TimeSpan.Zero));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/sync/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SyncQueueDiagnosticsDto>();
        Assert.NotNull(payload);
        Assert.Equal(4, payload!.PendingCount);
        Assert.Equal(2, payload.FailedCount);
        Assert.Equal(7, payload.SyncedCount);
        Assert.Equal(1, payload.RetryDueCount);
        Assert.Equal(13, payload.TotalCount);
        Assert.Equal(new DateTimeOffset(2026, 4, 20, 9, 30, 0, TimeSpan.Zero), payload.OldestPendingEnqueuedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 21, 10, 15, 0, TimeSpan.Zero), payload.EvaluatedAtUtc);

        Assert.Equal(1, factory.FakeService.GetDiagnosticsCallCount);
    }

    private sealed class SyncApiFactory : WebApplicationFactory<Program>
    {
        public FakeSyncQueueService FakeService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISyncQueueService>();
                services.AddSingleton(FakeService);
                services.AddSingleton<ISyncQueueService>(provider =>
                    provider.GetRequiredService<FakeSyncQueueService>());
            });
        }
    }

    private sealed class FakeSyncQueueService : ISyncQueueService
    {
        public SyncQueueDiagnostics Diagnostics { get; set; } = new(
            PendingCount: 0,
            FailedCount: 0,
            SyncedCount: 0,
            RetryDueCount: 0,
            TotalCount: 0,
            OldestPendingEnqueuedAtUtc: null,
            EvaluatedAtUtc: DateTimeOffset.UtcNow);

        public int GetDiagnosticsCallCount { get; private set; }

        public Task<SyncQueueItem> EnqueueAsync(
            string entityType,
            Guid entityId,
            string operation,
            string payloadJson,
            DateTimeOffset entityUpdatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SyncQueueItem>>([]);
        }

        public Task<SyncQueueDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            GetDiagnosticsCallCount++;
            return Task.FromResult(Diagnostics);
        }

        public Task MarkSyncedAsync(
            Guid queueItemId,
            DateTimeOffset syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }

        public Task MarkFailedAsync(
            Guid queueItemId,
            string error,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed in this test.");
        }
    }
}
