namespace STailor.Shared.Contracts.Sync;

public sealed record SyncQueueDiagnosticsDto(
    int PendingCount,
    int FailedCount,
    int SyncedCount,
    int RetryDueCount,
    int TotalCount,
    DateTimeOffset? OldestPendingEnqueuedAtUtc,
    DateTimeOffset EvaluatedAtUtc);
