using STailor.Core.Application.Abstractions;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;
using STailor.Core.Common.Time;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Exceptions;

namespace STailor.Modules.Core.Services;

public sealed class SyncQueueService : ISyncQueueService
{
    private readonly ISyncQueueRepository _syncQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;

    public SyncQueueService(
        ISyncQueueRepository syncQueueRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock)
    {
        _syncQueueRepository = syncQueueRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _clock = clock;
    }

    public async Task<SyncQueueItem> EnqueueAsync(
        string entityType,
        Guid entityId,
        string operation,
        string payloadJson,
        DateTimeOffset entityUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = SyncQueueItem.ComposeIdempotencyKey(
            entityType,
            entityId,
            operation,
            entityUpdatedAtUtc);

        var existingItem = await _syncQueueRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingItem is not null)
        {
            return existingItem;
        }

        var nowUtc = _clock.UtcNow;
        var actor = _currentUserService.GetCurrentUserId();

        var queueItem = new SyncQueueItem(
            entityType,
            entityId,
            operation,
            payloadJson,
            entityUpdatedAtUtc,
            nowUtc);

        queueItem.StampCreated(nowUtc, actor);

        await _syncQueueRepository.AddAsync(queueItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return queueItem;
    }

    public Task<IReadOnlyList<SyncQueueItem>> GetPendingBatchAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new DomainRuleViolationException("Max items must be greater than zero.");
        }

        return _syncQueueRepository.GetPendingBatchAsync(maxItems, _clock.UtcNow, cancellationToken);
    }

    public Task<SyncQueueDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        return _syncQueueRepository.GetDiagnosticsAsync(_clock.UtcNow, cancellationToken);
    }

    public async Task MarkSyncedAsync(
        Guid queueItemId,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var queueItem = await _syncQueueRepository.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null)
        {
            throw new DomainRuleViolationException("Sync queue item was not found.");
        }

        queueItem.MarkSynced(syncedAtUtc);
        queueItem.StampUpdated(_clock.UtcNow, _currentUserService.GetCurrentUserId());

        await _syncQueueRepository.UpdateAsync(queueItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid queueItemId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var queueItem = await _syncQueueRepository.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null)
        {
            throw new DomainRuleViolationException("Sync queue item was not found.");
        }

        var nowUtc = _clock.UtcNow;
        queueItem.MarkAttemptFailed(nowUtc, error);
        queueItem.StampUpdated(nowUtc, _currentUserService.GetCurrentUserId());

        await _syncQueueRepository.UpdateAsync(queueItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
