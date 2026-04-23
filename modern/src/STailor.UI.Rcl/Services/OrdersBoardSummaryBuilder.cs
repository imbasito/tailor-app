using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardSummary(
    int TotalOrders,
    int NewCount,
    int InProgressCount,
    int TrialFittingCount,
    int ReworkCount,
    int ReadyCount,
    int DeliveredCount,
    int OverdueCount,
    int DueTodayCount,
    int AtRiskCount,
    int ReadyWithBalanceCount,
    decimal TotalCharged,
    decimal TotalPaid,
    decimal TotalBalanceDue,
    decimal CollectionRatePercent);

public static class OrdersBoardSummaryBuilder
{
    public static OrdersBoardSummary Empty { get; } = new(
        TotalOrders: 0,
        NewCount: 0,
        InProgressCount: 0,
        TrialFittingCount: 0,
        ReworkCount: 0,
        ReadyCount: 0,
        DeliveredCount: 0,
        OverdueCount: 0,
        DueTodayCount: 0,
        AtRiskCount: 0,
        ReadyWithBalanceCount: 0,
        TotalCharged: 0m,
        TotalPaid: 0m,
        TotalBalanceDue: 0m,
        CollectionRatePercent: 0m);

    public static OrdersBoardSummary Build(IReadOnlyList<OrderWorklistItemDto> items, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Empty;
        }

        var todayUtc = utcNow.Date;
        var totalCharged = 0m;
        var totalPaid = 0m;
        var totalBalance = 0m;

        var newCount = 0;
        var inProgressCount = 0;
        var trialFittingCount = 0;
        var reworkCount = 0;
        var readyCount = 0;
        var deliveredCount = 0;
        var overdueCount = 0;
        var dueTodayCount = 0;
        var atRiskCount = 0;
        var readyWithBalanceCount = 0;

        foreach (var item in items)
        {
            totalCharged += item.AmountCharged;
            totalPaid += item.AmountPaid;
            totalBalance += item.BalanceDue;

            var canonicalStatus = OrdersBoardFilterStateMapper.NormalizeStatus(item.Status);
            switch (canonicalStatus)
            {
                case "New":
                    newCount++;
                    break;
                case "InProgress":
                    inProgressCount++;
                    break;
                case "TrialFitting":
                    trialFittingCount++;
                    break;
                case "Rework":
                    reworkCount++;
                    break;
                case "Ready":
                    readyCount++;
                    break;
                case "Delivered":
                    deliveredCount++;
                    break;
            }

            var dueDateUtc = item.DueAtUtc.UtcDateTime.Date;
            var isDelivered = string.Equals(canonicalStatus, "Delivered", StringComparison.Ordinal);
            if (!isDelivered && dueDateUtc < todayUtc)
            {
                overdueCount++;
                atRiskCount++;
            }

            if (!isDelivered && dueDateUtc == todayUtc)
            {
                dueTodayCount++;
                atRiskCount++;
            }

            if (string.Equals(canonicalStatus, "Ready", StringComparison.Ordinal) && item.BalanceDue > 0m)
            {
                readyWithBalanceCount++;
            }
        }

        var collectionRatePercent = totalCharged > 0m
            ? decimal.Round((totalPaid / totalCharged) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new OrdersBoardSummary(
            TotalOrders: items.Count,
            NewCount: newCount,
            InProgressCount: inProgressCount,
            TrialFittingCount: trialFittingCount,
            ReworkCount: reworkCount,
            ReadyCount: readyCount,
            DeliveredCount: deliveredCount,
            OverdueCount: overdueCount,
            DueTodayCount: dueTodayCount,
            AtRiskCount: atRiskCount,
            ReadyWithBalanceCount: readyWithBalanceCount,
            TotalCharged: totalCharged,
            TotalPaid: totalPaid,
            TotalBalanceDue: totalBalance,
            CollectionRatePercent: collectionRatePercent);
    }
}
