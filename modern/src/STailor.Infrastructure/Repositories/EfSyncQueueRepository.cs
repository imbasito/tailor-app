using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.ReadModels;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Repositories;

public sealed class EfSyncQueueRepository : ISyncQueueRepository
{
    private readonly LocalTailorDbContext _dbContext;

    public EfSyncQueueRepository(LocalTailorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.SyncQueueItems.AddAsync(item, cancellationToken);
    }

    public async Task<SyncQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SyncQueueItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<SyncQueueItem?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SyncQueueItems
            .FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(
        int maxItems,
        DateTimeOffset dueOnOrBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SyncQueueItems
            .Where(item =>
                item.Status == SyncQueueStatus.Pending
                || (item.Status == SyncQueueStatus.Failed
                    && (!item.NextAttemptAtUtc.HasValue || item.NextAttemptAtUtc <= dueOnOrBeforeUtc)))
            .OrderBy(item => item.NextAttemptAtUtc ?? item.EnqueuedAtUtc)
            .ThenBy(item => item.EnqueuedAtUtc)
            .ThenBy(item => item.AttemptCount)
            .Take(maxItems)
            .ToListAsync(cancellationToken);
    }

    public async Task<SyncQueueDiagnostics> GetDiagnosticsAsync(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var statusCounts = await _dbContext.SyncQueueItems
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var pendingCount = statusCounts
            .FirstOrDefault(item => item.Status == SyncQueueStatus.Pending)
            ?.Count ?? 0;

        var failedCount = statusCounts
            .FirstOrDefault(item => item.Status == SyncQueueStatus.Failed)
            ?.Count ?? 0;

        var syncedCount = statusCounts
            .FirstOrDefault(item => item.Status == SyncQueueStatus.Synced)
            ?.Count ?? 0;

        var retryDueCount = await _dbContext.SyncQueueItems
            .CountAsync(
                item => item.Status == SyncQueueStatus.Failed
                        && (!item.NextAttemptAtUtc.HasValue || item.NextAttemptAtUtc <= evaluatedAtUtc),
                cancellationToken);

        var oldestPendingEnqueuedAtUtc = await _dbContext.SyncQueueItems
            .Where(item => item.Status == SyncQueueStatus.Pending || item.Status == SyncQueueStatus.Failed)
            .OrderBy(item => item.EnqueuedAtUtc)
            .Select(item => (DateTimeOffset?)item.EnqueuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new SyncQueueDiagnostics(
            PendingCount: pendingCount,
            FailedCount: failedCount,
            SyncedCount: syncedCount,
            RetryDueCount: retryDueCount,
            TotalCount: pendingCount + failedCount + syncedCount,
            OldestPendingEnqueuedAtUtc: oldestPendingEnqueuedAtUtc,
            EvaluatedAtUtc: evaluatedAtUtc);
    }

    public Task UpdateAsync(SyncQueueItem item, CancellationToken cancellationToken = default)
    {
        _dbContext.SyncQueueItems.Update(item);
        return Task.CompletedTask;
    }
}
