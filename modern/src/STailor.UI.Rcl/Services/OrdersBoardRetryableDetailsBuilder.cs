namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardRetryableDetailItem(
    Guid OrderId,
    string CustomerName,
    string TargetStatus);

public sealed record OrdersBoardRetryableDetailsPreview(
    IReadOnlyList<OrdersBoardRetryableDetailItem> Items,
    int HiddenCount)
{
    public bool HasItems => Items.Count > 0;

    public bool IsTruncated => HiddenCount > 0;
}

public static class OrdersBoardRetryableDetailsBuilder
{
    public static OrdersBoardRetryableDetailsPreview Build(
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate>? candidates,
        int maxItems = 12)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return new OrdersBoardRetryableDetailsPreview([], 0);
        }

        var boundedMaxItems = Math.Max(1, maxItems);

        var sortedCandidates = candidates
            .OrderBy(candidate => GetStatusOrder(candidate.TargetStatus))
            .ThenBy(candidate => OrdersBoardFilterStateMapper.NormalizeStatus(candidate.TargetStatus), StringComparer.Ordinal)
            .ThenBy(candidate => NormalizeCustomerName(candidate.CustomerName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.OrderId)
            .ToList();

        var items = sortedCandidates
            .Take(boundedMaxItems)
            .Select(candidate => new OrdersBoardRetryableDetailItem(
                candidate.OrderId,
                NormalizeCustomerName(candidate.CustomerName),
                candidate.TargetStatus))
            .ToList();

        var hiddenCount = Math.Max(0, sortedCandidates.Count - items.Count);

        return new OrdersBoardRetryableDetailsPreview(items, hiddenCount);
    }

    private static string NormalizeCustomerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown Customer";
        }

        return value.Trim();
    }

    private static int GetStatusOrder(string status)
    {
        var normalizedStatus = OrdersBoardFilterStateMapper.NormalizeStatus(status);

        return normalizedStatus switch
        {
            "InProgress" => 0,
            "TrialFitting" => 1,
            "Rework" => 2,
            "Ready" => 3,
            "Delivered" => 4,
            _ => 9,
        };
    }
}