using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardBulkAdvanceRetryCandidate(
    Guid OrderId,
    string CustomerName,
    string TargetStatus);

public static class OrdersBoardBulkAdvanceRetryPlanner
{
    public static IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> BuildSafeRetryCandidates(
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate>? failedCandidates,
        IReadOnlyList<OrderWorklistItemDto> currentItems)
    {
        ArgumentNullException.ThrowIfNull(currentItems);

        if (failedCandidates is null || failedCandidates.Count == 0)
        {
            return [];
        }

        var itemsByOrderId = currentItems
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.First());

        var safeCandidates = new List<OrdersBoardBulkAdvanceRetryCandidate>();
        foreach (var failedCandidate in failedCandidates)
        {
            if (!itemsByOrderId.TryGetValue(failedCandidate.OrderId, out var currentItem))
            {
                continue;
            }

            if (!OrdersBoardBulkAdvancePlanner.TryGetNextStatus(currentItem.Status, out var currentTargetStatus))
            {
                continue;
            }

            if (!string.Equals(currentTargetStatus, failedCandidate.TargetStatus, StringComparison.Ordinal))
            {
                continue;
            }

            safeCandidates.Add(new OrdersBoardBulkAdvanceRetryCandidate(
                failedCandidate.OrderId,
                currentItem.CustomerName,
                failedCandidate.TargetStatus));
        }

        return safeCandidates
            .OrderBy(candidate => GetStatusOrder(candidate.TargetStatus))
            .ThenBy(candidate => OrdersBoardFilterStateMapper.NormalizeStatus(candidate.TargetStatus), StringComparer.Ordinal)
            .ThenBy(candidate => NormalizeCustomerName(candidate.CustomerName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.OrderId)
            .ToList();
    }

    public static OrdersBoardBulkAdvancePreview BuildPreview(
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> candidates,
        int maxItems = 5)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return OrdersBoardBulkAdvancePlanner.BuildPreview(ToPlan(candidates), maxItems);
    }

    public static IReadOnlyList<OrdersBoardBulkAdvanceTargetDistributionItem> BuildTargetDistribution(
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return OrdersBoardBulkAdvancePlanner.BuildTargetDistribution(ToPlan(candidates));
    }

    private static OrdersBoardBulkAdvancePlan ToPlan(IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> candidates)
    {
        var planCandidates = candidates
            .Select(candidate => new OrdersBoardBulkAdvanceCandidate(
                candidate.OrderId,
                candidate.CustomerName,
                CurrentStatus: string.Empty,
                candidate.TargetStatus))
            .ToList();

        return new OrdersBoardBulkAdvancePlan(
            OrdersBoardWorklistGroupKind.New,
            "Retry",
            planCandidates);
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

    private static string NormalizeCustomerName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown Customer"
            : value.Trim();
    }
}
