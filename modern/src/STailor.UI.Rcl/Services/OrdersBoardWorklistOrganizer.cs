using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public enum OrdersBoardWorklistGroupKind
{
    New,
    InProgress,
    TrialFitting,
    Rework,
    Ready,
    Delivered,

    // Backward-compatible query/test aliases from the old urgency grouping model.
    Overdue,
    DueToday,
    ReadyWithBalance,
    Upcoming,
}

public sealed record OrdersBoardWorklistGroup(
    OrdersBoardWorklistGroupKind Kind,
    string Title,
    IReadOnlyList<OrderWorklistItemDto> Items);

public static class OrdersBoardWorklistOrganizer
{
    public static IReadOnlyList<OrdersBoardWorklistGroup> BuildGroups(
        IReadOnlyList<OrderWorklistItemDto> items,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return [];
        }

        var orderedItems = items
            .Select(item => new
            {
                Item = item,
                Kind = GetGroupKind(item),
            })
            .OrderBy(entry => GetGroupOrder(entry.Kind))
            .ThenBy(entry => entry.Item.DueAtUtc)
            .ThenByDescending(entry => entry.Item.BalanceDue)
            .ThenBy(entry => entry.Item.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return orderedItems
            .GroupBy(entry => entry.Kind)
            .OrderBy(group => GetGroupOrder(group.Key))
            .Select(group => new OrdersBoardWorklistGroup(
                Kind: group.Key,
                Title: GetGroupTitle(group.Key),
                Items: group.Select(entry => entry.Item).ToList()))
            .ToList();
    }

    private static OrdersBoardWorklistGroupKind GetGroupKind(OrderWorklistItemDto item)
    {
        var canonicalStatus = OrdersBoardFilterStateMapper.NormalizeStatus(item.Status);
        return canonicalStatus switch
        {
            "InProgress" => OrdersBoardWorklistGroupKind.InProgress,
            "TrialFitting" => OrdersBoardWorklistGroupKind.TrialFitting,
            "Rework" => OrdersBoardWorklistGroupKind.Rework,
            "Ready" => OrdersBoardWorklistGroupKind.Ready,
            "Delivered" => OrdersBoardWorklistGroupKind.Delivered,
            _ => OrdersBoardWorklistGroupKind.New,
        };
    }

    private static string GetGroupTitle(OrdersBoardWorklistGroupKind kind)
    {
        return kind switch
        {
            OrdersBoardWorklistGroupKind.New => "New",
            OrdersBoardWorklistGroupKind.InProgress => "In Progress",
            OrdersBoardWorklistGroupKind.TrialFitting => "Fitting",
            OrdersBoardWorklistGroupKind.Rework => "Rework",
            OrdersBoardWorklistGroupKind.Ready => "Ready",
            OrdersBoardWorklistGroupKind.Delivered => "Delivered",
            _ => "Other",
        };
    }

    private static int GetGroupOrder(OrdersBoardWorklistGroupKind kind)
    {
        return kind switch
        {
            OrdersBoardWorklistGroupKind.New => 0,
            OrdersBoardWorklistGroupKind.InProgress => 1,
            OrdersBoardWorklistGroupKind.TrialFitting => 2,
            OrdersBoardWorklistGroupKind.Rework => 3,
            OrdersBoardWorklistGroupKind.Ready => 4,
            OrdersBoardWorklistGroupKind.Delivered => 5,
            _ => 9,
        };
    }
}
