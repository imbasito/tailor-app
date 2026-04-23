using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardBulkAdvanceCandidate(
    Guid OrderId,
    string CustomerName,
    string CurrentStatus,
    string TargetStatus);

public sealed record OrdersBoardBulkAdvancePlan(
    OrdersBoardWorklistGroupKind GroupKind,
    string GroupTitle,
    IReadOnlyList<OrdersBoardBulkAdvanceCandidate> Candidates)
{
    public int EligibleCount => Candidates.Count;

    public bool HasEligible => EligibleCount > 0;
}

public sealed record OrdersBoardBulkAdvancePreviewItem(
    string CustomerName,
    string TargetStatus);

public sealed record OrdersBoardBulkAdvancePreview(
    IReadOnlyList<OrdersBoardBulkAdvancePreviewItem> Items,
    int HiddenCount)
{
    public bool IsTruncated => HiddenCount > 0;
}

public sealed record OrdersBoardBulkAdvanceTargetDistributionItem(
    string TargetStatus,
    int Count);

public static class OrdersBoardBulkAdvancePlanner
{
    public static OrdersBoardBulkAdvancePlan BuildPlan(OrdersBoardWorklistGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var candidates = new List<OrdersBoardBulkAdvanceCandidate>();
        foreach (var item in group.Items)
        {
            if (!TryGetNextStatus(item.Status, out var nextStatus))
            {
                continue;
            }

            candidates.Add(new OrdersBoardBulkAdvanceCandidate(
                item.OrderId,
                item.CustomerName,
                item.Status,
                nextStatus!));
        }

        return new OrdersBoardBulkAdvancePlan(group.Kind, group.Title, candidates);
    }

    public static bool TryGetNextStatus(string currentStatus, out string? nextStatus)
    {
        var canonicalStatus = OrdersBoardFilterStateMapper.NormalizeStatus(currentStatus);

        nextStatus = canonicalStatus switch
        {
            "New" => "InProgress",
            "InProgress" => "TrialFitting",
            "TrialFitting" => "Rework",
            "Rework" => "Ready",
            "Ready" => "Delivered",
            _ => null,
        };

        return nextStatus is not null;
    }

    public static OrdersBoardBulkAdvancePreview BuildPreview(
        OrdersBoardBulkAdvancePlan plan,
        int maxItems = 5)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var boundedMaxItems = Math.Max(1, maxItems);

        var items = plan.Candidates
            .Take(boundedMaxItems)
            .Select(candidate => new OrdersBoardBulkAdvancePreviewItem(
                candidate.CustomerName,
                candidate.TargetStatus))
            .ToList();

        var hiddenCount = Math.Max(0, plan.EligibleCount - items.Count);

        return new OrdersBoardBulkAdvancePreview(items, hiddenCount);
    }

    public static IReadOnlyList<OrdersBoardBulkAdvanceTargetDistributionItem> BuildTargetDistribution(
        OrdersBoardBulkAdvancePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Candidates
            .GroupBy(candidate => OrdersBoardFilterStateMapper.NormalizeStatus(candidate.TargetStatus))
            .Select(group => new OrdersBoardBulkAdvanceTargetDistributionItem(group.Key, group.Count()))
            .OrderBy(item => GetStatusOrder(item.TargetStatus))
            .ThenBy(item => item.TargetStatus, StringComparer.Ordinal)
            .ToList();
    }

    private static int GetStatusOrder(string status)
    {
        return status switch
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