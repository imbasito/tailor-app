namespace STailor.UI.Rcl.Services;

public static class OrdersBoardBulkResultMetaBuilder
{
    public static DateTimeOffset? ResolveLastFailedAtUtc(
        DateTimeOffset? previousLastFailedAtUtc,
        int failedCount,
        DateTimeOffset occurredAtUtc)
    {
        if (failedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedCount));
        }

        return failedCount > 0
            ? occurredAtUtc
            : previousLastFailedAtUtc;
    }

    public static string BuildRetryableNowLabel(int retryableNowCount)
    {
        if (retryableNowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryableNowCount));
        }

        return $"Retryable now: {retryableNowCount}";
    }
}