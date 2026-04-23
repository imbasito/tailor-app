namespace STailor.UI.Rcl.Services;

public sealed class OrdersBoardGroupUiState
{
    private static readonly Dictionary<string, OrdersBoardWorklistGroupKind> GroupAliases = new(StringComparer.Ordinal)
    {
        ["new"] = OrdersBoardWorklistGroupKind.New,
        ["inprogress"] = OrdersBoardWorklistGroupKind.InProgress,
        ["progress"] = OrdersBoardWorklistGroupKind.InProgress,
        ["trialfitting"] = OrdersBoardWorklistGroupKind.TrialFitting,
        ["fitting"] = OrdersBoardWorklistGroupKind.TrialFitting,
        ["rework"] = OrdersBoardWorklistGroupKind.Rework,
        ["ready"] = OrdersBoardWorklistGroupKind.Ready,
        ["delivered"] = OrdersBoardWorklistGroupKind.Delivered,
    };

    private readonly HashSet<OrdersBoardWorklistGroupKind> _collapsedGroups = [];

    public bool IsCollapsed(OrdersBoardWorklistGroupKind kind)
    {
        return _collapsedGroups.Contains(kind);
    }

    public bool Toggle(OrdersBoardWorklistGroupKind kind)
    {
        if (_collapsedGroups.Add(kind))
        {
            return true;
        }

        _collapsedGroups.Remove(kind);
        return false;
    }

    public IReadOnlyList<OrdersBoardWorklistGroupKind> GetCollapsedKinds()
    {
        return _collapsedGroups
            .OrderBy(GetGroupOrder)
            .ToList();
    }

    public void SetCollapsedKinds(IEnumerable<OrdersBoardWorklistGroupKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        _collapsedGroups.Clear();
        foreach (var kind in kinds)
        {
            _collapsedGroups.Add(kind);
        }
    }

    public void SyncToGroups(IReadOnlyList<OrdersBoardWorklistGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var validKinds = groups
            .Select(group => group.Kind)
            .ToHashSet();

        _collapsedGroups.RemoveWhere(kind => !validKinds.Contains(kind));
    }

    public void ExpandAll()
    {
        _collapsedGroups.Clear();
    }

    public static IReadOnlyList<OrdersBoardWorklistGroupKind> ParseCollapsedGroupsQuery(string? queryValue)
    {
        if (string.IsNullOrWhiteSpace(queryValue))
        {
            return [];
        }

        var separators = new[] { ',', ';', '|', ' ' };

        return queryValue
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(token => GroupAliases.ContainsKey(token))
            .Select(token => GroupAliases[token])
            .Distinct()
            .OrderBy(GetGroupOrder)
            .ToList();
    }

    public static string? BuildCollapsedGroupsQueryValue(IEnumerable<OrdersBoardWorklistGroupKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        var orderedKinds = kinds
            .Distinct()
            .OrderBy(GetGroupOrder)
            .Select(kind => kind.ToString())
            .ToList();

        return orderedKinds.Count == 0
            ? null
            : string.Join(',', orderedKinds);
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var index = 0;

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            buffer[index++] = char.ToLowerInvariant(character);
        }

        return index == 0
            ? string.Empty
            : new string(buffer, 0, index);
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
