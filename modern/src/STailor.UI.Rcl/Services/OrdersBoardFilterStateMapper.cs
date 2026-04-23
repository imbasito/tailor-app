using System.Globalization;

namespace STailor.UI.Rcl.Services;

public sealed record OrdersBoardFilterState(
    string ApiBaseUrl,
    int MaxItems,
    bool IncludeDelivered,
    bool OverdueOnly,
    string StatusFilter,
    DateTime? DueOnOrBeforeDate);

public enum OrdersBoardQuickPreset
{
    TodayDue,
    Late,
    DueThisWeek,
    Unpaid,
    Ready,
    Delivered,

    // Backward-compatible aliases for older URLs/tests.
    ReadyOnly = Ready,
    Overdue = Late,
}

public static class OrdersBoardFilterStateMapper
{
    public const string AnyStatus = "Any";
    private const string NewStatus = "New";
    private const string InProgressStatus = "InProgress";
    private const string TrialFittingStatus = "TrialFitting";
    private const string ReworkStatus = "Rework";
    private const string ReadyStatus = "Ready";
    private const string DeliveredStatus = "Delivered";

    private const int MinMaxItems = 1;
    private const int MaxMaxItems = 500;

    private static readonly Dictionary<string, string> StatusAliasMap = new(StringComparer.Ordinal)
    {
        ["any"] = AnyStatus,
        ["all"] = AnyStatus,
        ["new"] = NewStatus,
        ["inprogress"] = InProgressStatus,
        ["progress"] = InProgressStatus,
        ["trialfitting"] = TrialFittingStatus,
        ["trialfit"] = TrialFittingStatus,
        ["fitting"] = TrialFittingStatus,
        ["rework"] = ReworkStatus,
        ["ready"] = ReadyStatus,
        ["delivered"] = DeliveredStatus,
    };

    private static readonly Dictionary<string, OrdersBoardQuickPreset> PresetAliasMap = new(StringComparer.Ordinal)
    {
        ["todaydue"] = OrdersBoardQuickPreset.TodayDue,
        ["today"] = OrdersBoardQuickPreset.TodayDue,
        ["late"] = OrdersBoardQuickPreset.Late,
        ["overdue"] = OrdersBoardQuickPreset.Late,
        ["duethisweek"] = OrdersBoardQuickPreset.DueThisWeek,
        ["week"] = OrdersBoardQuickPreset.DueThisWeek,
        ["unpaid"] = OrdersBoardQuickPreset.Unpaid,
        ["balance"] = OrdersBoardQuickPreset.Unpaid,
        ["ready"] = OrdersBoardQuickPreset.Ready,
        ["readyonly"] = OrdersBoardQuickPreset.Ready,
        ["delivered"] = OrdersBoardQuickPreset.Delivered,
    };

    public static OrdersBoardFilterState FromQuery(
        string apiBaseUrl,
        int defaultMaxItems,
        bool defaultIncludeDelivered,
        bool defaultOverdueOnly,
        string defaultStatusFilter,
        int? maxItems,
        bool? includeDelivered,
        bool? overdueOnly,
        string? status,
        string? dueOnOrBefore)
    {
        var normalizedMaxItems = maxItems is >= MinMaxItems and <= MaxMaxItems
            ? maxItems.Value
            : defaultMaxItems;

        return new OrdersBoardFilterState(
            ApiBaseUrl: apiBaseUrl,
            MaxItems: normalizedMaxItems,
            IncludeDelivered: includeDelivered ?? defaultIncludeDelivered,
            OverdueOnly: overdueOnly ?? defaultOverdueOnly,
            StatusFilter: NormalizeStatus(status ?? defaultStatusFilter),
            DueOnOrBeforeDate: TryParseDate(dueOnOrBefore));
    }

    public static IReadOnlyDictionary<string, object?> ToQueryParameters(
        OrdersBoardFilterState filters,
        OrdersBoardFilterState? defaults = null)
    {
        var normalizedStatus = NormalizeStatus(filters.StatusFilter);

        if (defaults is null)
        {
            return new Dictionary<string, object?>
            {
                ["maxItems"] = filters.MaxItems,
                ["includeDelivered"] = filters.IncludeDelivered,
                ["overdueOnly"] = filters.OverdueOnly,
                ["status"] = normalizedStatus,
                ["dueOnOrBefore"] = filters.DueOnOrBeforeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
        }

        var normalizedDefaultStatus = NormalizeStatus(defaults.StatusFilter);

        return new Dictionary<string, object?>
        {
            ["maxItems"] = filters.MaxItems == defaults.MaxItems
                ? null
                : filters.MaxItems,
            ["includeDelivered"] = filters.IncludeDelivered == defaults.IncludeDelivered
                ? null
                : filters.IncludeDelivered,
            ["overdueOnly"] = filters.OverdueOnly == defaults.OverdueOnly
                ? null
                : filters.OverdueOnly,
            ["status"] = string.Equals(normalizedStatus, normalizedDefaultStatus, StringComparison.Ordinal)
                ? null
                : normalizedStatus,
            ["dueOnOrBefore"] = filters.DueOnOrBeforeDate == defaults.DueOnOrBeforeDate
                ? null
                : filters.DueOnOrBeforeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    public static string NormalizeStatus(string? status)
    {
        var statusToken = NormalizeStatusToken(status);
        return StatusAliasMap.TryGetValue(statusToken, out var canonicalStatus)
            ? canonicalStatus
            : AnyStatus;
    }

    public static OrdersBoardQuickPreset? ParsePreset(string? preset)
    {
        var presetToken = NormalizePresetToken(preset);
        return PresetAliasMap.TryGetValue(presetToken, out var value)
            ? value
            : null;
    }

    public static string? ToPresetQueryValue(OrdersBoardQuickPreset? preset)
    {
        return preset?.ToString();
    }

    public static string BuildQueryFingerprint(
        int? maxItems,
        bool? includeDelivered,
        bool? overdueOnly,
        string? status,
        string? dueOnOrBefore)
    {
        var normalizedDueDate = TryParseDate(dueOnOrBefore)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? string.Empty;

        return string.Join(
            "|",
            maxItems?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            includeDelivered?.ToString() ?? string.Empty,
            overdueOnly?.ToString() ?? string.Empty,
            NormalizeStatus(status),
            normalizedDueDate);
    }

    public static OrdersBoardFilterState ApplyPreset(
        OrdersBoardFilterState current,
        OrdersBoardQuickPreset preset,
        DateTime utcNow)
    {
        return preset switch
        {
            OrdersBoardQuickPreset.TodayDue => current with
            {
                IncludeDelivered = false,
                OverdueOnly = false,
                StatusFilter = AnyStatus,
                DueOnOrBeforeDate = utcNow.Date,
            },
            OrdersBoardQuickPreset.Late => current with
            {
                IncludeDelivered = false,
                OverdueOnly = true,
                StatusFilter = AnyStatus,
                DueOnOrBeforeDate = null,
            },
            OrdersBoardQuickPreset.DueThisWeek => current with
            {
                IncludeDelivered = false,
                OverdueOnly = false,
                StatusFilter = AnyStatus,
                DueOnOrBeforeDate = utcNow.Date.AddDays(7),
            },
            OrdersBoardQuickPreset.Unpaid => current with
            {
                IncludeDelivered = true,
                OverdueOnly = false,
                StatusFilter = AnyStatus,
                DueOnOrBeforeDate = null,
            },
            OrdersBoardQuickPreset.Ready => current with
            {
                IncludeDelivered = false,
                OverdueOnly = false,
                StatusFilter = "Ready",
                DueOnOrBeforeDate = null,
            },
            OrdersBoardQuickPreset.Delivered => current with
            {
                IncludeDelivered = true,
                OverdueOnly = false,
                StatusFilter = "Delivered",
                DueOnOrBeforeDate = null,
            },
            _ => current,
        };
    }

    public static bool MatchesPreset(
        OrdersBoardFilterState current,
        OrdersBoardQuickPreset preset,
        DateTime utcNow)
    {
        var normalizedCurrent = current with
        {
            StatusFilter = NormalizeStatus(current.StatusFilter),
            DueOnOrBeforeDate = current.DueOnOrBeforeDate?.Date,
        };

        var expected = ApplyPreset(normalizedCurrent, preset, utcNow);

        return normalizedCurrent.IncludeDelivered == expected.IncludeDelivered
            && normalizedCurrent.OverdueOnly == expected.OverdueOnly
            && string.Equals(normalizedCurrent.StatusFilter, expected.StatusFilter, StringComparison.Ordinal)
            && normalizedCurrent.DueOnOrBeforeDate == expected.DueOnOrBeforeDate;
    }

    public static DateTimeOffset? ToDueOnOrBeforeUtc(DateTime? dueOnOrBeforeDate)
    {
        return dueOnOrBeforeDate is null
            ? null
            : new DateTimeOffset(dueOnOrBeforeDate.Value.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);
    }

    private static DateTime? TryParseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        return DateTime.TryParseExact(
            date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate.Date
            : null;
    }

    private static string NormalizeStatusToken(string? status)
    {
        return NormalizeToken(status);
    }

    private static string NormalizePresetToken(string? preset)
    {
        return NormalizeToken(preset);
    }

    private static string NormalizeToken(string? value)
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
}
