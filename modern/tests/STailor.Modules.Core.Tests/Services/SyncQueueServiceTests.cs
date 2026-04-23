using STailor.Core.Domain.Enums;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Tests.Fakes;

namespace STailor.Modules.Core.Tests.Services;

public sealed class SyncQueueServiceTests
{
    [Fact]
    public async Task EnqueueAsync_AddsPendingItemAndStampsAudit()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var item = await service.EnqueueAsync(
            entityType: "Order",
            entityId: Guid.NewGuid(),
            operation: "upsert",
            payloadJson: "{\"id\":1}",
            entityUpdatedAtUtc: clock.UtcNow.AddMinutes(-2));

        Assert.Equal(SyncQueueStatus.Pending, item.Status);
        Assert.Equal("sync-worker", item.CreatedBy);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task MarkFailedAsync_UpdatesStatusAndAttemptCount()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var item = await service.EnqueueAsync(
            "CustomerProfile",
            Guid.NewGuid(),
            "upsert",
            "{\"id\":2}",
            clock.UtcNow);

        await service.MarkFailedAsync(item.Id, "Network unavailable");

        var updated = await repository.GetByIdAsync(item.Id);

        Assert.NotNull(updated);
        Assert.Equal(SyncQueueStatus.Failed, updated!.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.Equal("Network unavailable", updated.LastError);
        Assert.Equal(clock.UtcNow.AddMinutes(1), updated.NextAttemptAtUtc);
    }

    [Fact]
    public async Task MarkSyncedAsync_UpdatesStatusToSynced()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var item = await service.EnqueueAsync(
            "Order",
            Guid.NewGuid(),
            "upsert",
            "{\"id\":3}",
            clock.UtcNow);

        var syncedAt = clock.UtcNow.AddMinutes(10);
        await service.MarkSyncedAsync(item.Id, syncedAt);

        var updated = await repository.GetByIdAsync(item.Id);

        Assert.NotNull(updated);
        Assert.Equal(SyncQueueStatus.Synced, updated!.Status);
        Assert.Equal(syncedAt, updated.SyncedAtUtc);
    }

    [Fact]
    public async Task GetPendingBatchAsync_ReturnsOnlyPendingOrFailed()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var first = await service.EnqueueAsync("Order", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);
        var second = await service.EnqueueAsync("CustomerProfile", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);

        await service.MarkSyncedAsync(second.Id, clock.UtcNow.AddMinutes(2));
        await service.MarkFailedAsync(first.Id, "Temporary error");

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var pending = await service.GetPendingBatchAsync(10);

        Assert.Single(pending);
        Assert.Equal(first.Id, pending[0].Id);
        Assert.Equal(SyncQueueStatus.Failed, pending[0].Status);
    }

    [Fact]
    public async Task EnqueueAsync_WithDuplicateIdempotencyPayload_ReturnsExistingItem()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var entityId = Guid.NewGuid();
        var entityUpdatedAtUtc = clock.UtcNow.AddMinutes(-5);

        var first = await service.EnqueueAsync(
            entityType: "Order",
            entityId: entityId,
            operation: "upsert",
            payloadJson: "{\"id\":1}",
            entityUpdatedAtUtc: entityUpdatedAtUtc);

        var second = await service.EnqueueAsync(
            entityType: "Order",
            entityId: entityId,
            operation: "upsert",
            payloadJson: "{\"id\":1}",
            entityUpdatedAtUtc: entityUpdatedAtUtc);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetPendingBatchAsync_ExcludesFailedItemsUntilBackoffWindowIsDue()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var item = await service.EnqueueAsync(
            "Order",
            Guid.NewGuid(),
            "upsert",
            "{}",
            clock.UtcNow);

        await service.MarkFailedAsync(item.Id, "Central unavailable");

        var immediateBatch = await service.GetPendingBatchAsync(10);
        Assert.Empty(immediateBatch);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var dueBatch = await service.GetPendingBatchAsync(10);

        var dueItem = Assert.Single(dueBatch);
        Assert.Equal(item.Id, dueItem.Id);
        Assert.Equal(SyncQueueStatus.Failed, dueItem.Status);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_ReturnsCountsAndRetryDue()
    {
        var repository = new InMemorySyncQueueRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero));

        var service = new SyncQueueService(
            repository,
            unitOfWork,
            new FakeCurrentUserService("sync-worker"),
            clock);

        var failedDue = await service.EnqueueAsync("Order", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);
        await service.MarkFailedAsync(failedDue.Id, "Temporary error");

        var failedNotDueYet = await service.EnqueueAsync("Order", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);
        await service.MarkFailedAsync(failedNotDueYet.Id, "Temporary error");

        var pending = await service.EnqueueAsync("CustomerProfile", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);
        var synced = await service.EnqueueAsync("Order", Guid.NewGuid(), "upsert", "{}", clock.UtcNow);
        await service.MarkSyncedAsync(synced.Id, clock.UtcNow.AddMinutes(2));

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var diagnostics = await service.GetDiagnosticsAsync();

        Assert.Equal(1, diagnostics.PendingCount);
        Assert.Equal(2, diagnostics.FailedCount);
        Assert.Equal(1, diagnostics.SyncedCount);
        Assert.Equal(2, diagnostics.RetryDueCount);
        Assert.Equal(4, diagnostics.TotalCount);
        Assert.Equal(pending.EnqueuedAtUtc, diagnostics.OldestPendingEnqueuedAtUtc);
        Assert.Equal(clock.UtcNow, diagnostics.EvaluatedAtUtc);
    }
}
