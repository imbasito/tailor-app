namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardBulkFailedCustomersPreview(
    IReadOnlyList<string> Names,
    int HiddenCount)
{
    public bool HasItems => Names.Count > 0;

    public bool IsTruncated => HiddenCount > 0;
}

public static class OrdersBoardBulkFailurePreviewBuilder
{
    public static OrdersBoardBulkFailedCustomersPreview Build(
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate>? failedCandidates,
        int maxItems = 3)
    {
        if (failedCandidates is null || failedCandidates.Count == 0)
        {
            return new OrdersBoardBulkFailedCustomersPreview([], 0);
        }

        var boundedMaxItems = Math.Max(1, maxItems);

        var normalizedNames = failedCandidates
            .Select(candidate => candidate.CustomerName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedNames.Count == 0)
        {
            return new OrdersBoardBulkFailedCustomersPreview([], 0);
        }

        var names = normalizedNames
            .Take(boundedMaxItems)
            .ToList();

        var hiddenCount = Math.Max(0, normalizedNames.Count - names.Count);

        return new OrdersBoardBulkFailedCustomersPreview(names, hiddenCount);
    }
}