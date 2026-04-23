namespace STailor.Core.Application.ReadModels;

public sealed record SyncQueueDiagnostics(
    int PendingCount,
    int FailedCount,
    int SyncedCount,
    int RetryDueCount,
    int TotalCount,
    DateTimeOffset? OldestPendingEnqueuedAtUtc,
    DateTimeOffset EvaluatedAtUtc);