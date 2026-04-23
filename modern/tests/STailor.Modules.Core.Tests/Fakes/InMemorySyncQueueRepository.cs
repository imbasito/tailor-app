using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class InMemorySyncQueueRepository : ISyncQueueRepository
{
    private readonly Dictionary<Guid, SyncQueueItem> _store = new();

    public Task AddAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<SyncQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<SyncQueueItem?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var item = _store.Values.FirstOrDefault(
            current => string.Equals(current.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));

        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(
        int maxItems,
        DateTimeOffset dueOnOrBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(item =>
                item.Status == SyncQueueStatus.Pending
                || (item.Status == SyncQueueStatus.Failed
                    && (!item.NextAttemptAtUtc.HasValue || item.NextAttemptAtUtc <= dueOnOrBeforeUtc)))
            .OrderBy(item => item.NextAttemptAtUtc ?? item.EnqueuedAtUtc)
            .ThenBy(item => item.EnqueuedAtUtc)
            .ThenBy(item => item.AttemptCount)
            .Take(maxItems)
            .ToList();

        return Task.FromResult<IReadOnlyList<SyncQueueItem>>(items);
    }

    public Task<SyncQueueDiagnostics> GetDiagnosticsAsync(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var pendingCount = _store.Values.Count(item => item.Status == SyncQueueStatus.Pending);
        var failedCount = _store.Values.Count(item => item.Status == SyncQueueStatus.Failed);
        var syncedCount = _store.Values.Count(item => item.Status == SyncQueueStatus.Synced);
        var retryDueCount = _store.Values.Count(item =>
            item.Status == SyncQueueStatus.Failed
            && (!item.NextAttemptAtUtc.HasValue || item.NextAttemptAtUtc <= evaluatedAtUtc));
        var oldestPending = _store.Values
            .Where(item => item.Status == SyncQueueStatus.Pending || item.Status == SyncQueueStatus.Failed)
            .OrderBy(item => item.EnqueuedAtUtc)
            .Select(item => (DateTimeOffset?)item.EnqueuedAtUtc)
            .FirstOrDefault();

        return Task.FromResult(new SyncQueueDiagnostics(
            PendingCount: pendingCount,
            FailedCount: failedCount,
            SyncedCount: syncedCount,
            RetryDueCount: retryDueCount,
            TotalCount: pendingCount + failedCount + syncedCount,
            OldestPendingEnqueuedAtUtc: oldestPending,
            EvaluatedAtUtc: evaluatedAtUtc));
    }

    public Task UpdateAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }
}
