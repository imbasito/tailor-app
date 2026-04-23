using STailor.Core.Common.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;
using System.Globalization;

namespace STailor.Core.Domain.Entities;

public sealed class SyncQueueItem : AuditableEntity
{
    private SyncQueueItem()
    {
    }

    public SyncQueueItem(
        string entityType,
        Guid entityId,
        string operation,
        string payloadJson,
        DateTimeOffset entityUpdatedAtUtc,
        DateTimeOffset enqueuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainRuleViolationException("Entity type is required.");
        }

        if (entityId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Entity id is required.");
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new DomainRuleViolationException("Sync operation is required.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new DomainRuleViolationException("Payload json is required.");
        }

        EntityType = entityType.Trim();
        EntityId = entityId;
        Operation = operation.Trim();
        PayloadJson = payloadJson;
        EntityUpdatedAtUtc = entityUpdatedAtUtc;
        EnqueuedAtUtc = enqueuedAtUtc;
        NextAttemptAtUtc = enqueuedAtUtc;
        IdempotencyKey = ComposeIdempotencyKey(entityType, entityId, operation, entityUpdatedAtUtc);
        Status = SyncQueueStatus.Pending;
    }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset EntityUpdatedAtUtc { get; private set; }

    public DateTimeOffset EnqueuedAtUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? SyncedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public SyncQueueStatus Status { get; private set; }

    public static string ComposeIdempotencyKey(
        string entityType,
        Guid entityId,
        string operation,
        DateTimeOffset entityUpdatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainRuleViolationException("Entity type is required.");
        }

        if (entityId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Entity id is required.");
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new DomainRuleViolationException("Sync operation is required.");
        }

        var normalizedEntityType = entityType.Trim().ToLowerInvariant();
        var normalizedOperation = operation.Trim().ToLowerInvariant();
        var normalizedUpdatedAtUtc = entityUpdatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        return $"{normalizedEntityType}:{entityId:D}:{normalizedOperation}:{normalizedUpdatedAtUtc}";
    }

    public void MarkAttemptFailed(DateTimeOffset atUtc, string? error)
    {
        AttemptCount++;
        LastAttemptedAtUtc = atUtc;
        LastError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        Status = SyncQueueStatus.Failed;
        NextAttemptAtUtc = atUtc.Add(CalculateRetryDelay(AttemptCount));
    }

    public void MarkAttemptPending(DateTimeOffset atUtc)
    {
        AttemptCount++;
        LastAttemptedAtUtc = atUtc;
        LastError = null;
        Status = SyncQueueStatus.Pending;
        NextAttemptAtUtc = atUtc;
    }

    public void MarkSynced(DateTimeOffset syncedAtUtc)
    {
        SyncedAtUtc = syncedAtUtc;
        LastAttemptedAtUtc = syncedAtUtc;
        LastError = null;
        Status = SyncQueueStatus.Synced;
        NextAttemptAtUtc = null;
    }

    private static TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var boundedAttempt = Math.Clamp(attemptCount, 1, 8);
        var multiplier = Math.Pow(2, boundedAttempt - 1);
        var delaySeconds = Math.Min(1800d, 60d * multiplier);

        return TimeSpan.FromSeconds(delaySeconds);
    }
}
