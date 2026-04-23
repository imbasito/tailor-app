using STailor.Core.Domain.Entities;
using STailor.Core.Application.ReadModels;

namespace STailor.Core.Application.Abstractions.Repositories;

public interface ISyncQueueRepository
{
    Task AddAsync(SyncQueueItem item, CancellationToken cancellationToken = default);

    Task<SyncQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SyncQueueItem?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(
        int maxItems,
        DateTimeOffset dueOnOrBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<SyncQueueDiagnostics> GetDiagnosticsAsync(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SyncQueueItem item, CancellationToken cancellationToken = default);
}
