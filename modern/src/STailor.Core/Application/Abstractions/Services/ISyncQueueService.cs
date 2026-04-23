using STailor.Core.Domain.Entities;
using STailor.Core.Application.ReadModels;

namespace STailor.Core.Application.Abstractions.Services;

public interface ISyncQueueService
{
    Task<SyncQueueItem> EnqueueAsync(
        string entityType,
        Guid entityId,
        string operation,
        string payloadJson,
        DateTimeOffset entityUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(int maxItems, CancellationToken cancellationToken = default);

    Task<SyncQueueDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(Guid queueItemId, DateTimeOffset syncedAtUtc, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid queueItemId, string error, CancellationToken cancellationToken = default);
}
