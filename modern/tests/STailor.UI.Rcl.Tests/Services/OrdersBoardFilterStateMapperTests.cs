using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardFilterStateMapperTests
{
    [Fact]
    public void FromQuery_WithValidValues_ParsesAllFilters()
    {
        var filters = OrdersBoardFilterStateMapper.FromQuery(
            apiBaseUrl: "http://localhost:5064",
            defaultMaxItems: 80,
            defaultIncludeDelivered: false,
            defaultOverdueOnly: false,
            defaultStatusFilter: OrdersBoardFilterStateMapper.AnyStatus,
            maxItems: 25,
            includeDelivered: true,
            overdueOnly: true,
            status: "ready",
            dueOnOrBefore: "2026-05-02");

        Assert.Equal("http://localhost:5064", filters.ApiBaseUrl);
        Assert.Equal(25, filters.MaxItems);
        Assert.True(filters.IncludeDelivered);
        Assert.True(filters.OverdueOnly);
        Assert.Equal("Ready", filters.StatusFilter);
        Assert.Equal(new DateTime(2026, 5, 2), filters.DueOnOrBeforeDate);
    }

    [Fact]
    public void FromQuery_WithInvalidValues_FallsBackToDefaults()
    {
        var filters = OrdersBoardFilterStateMapper.FromQuery(
            apiBaseUrl: "http://localhost:5064",
            defaultMaxItems: 80,
            defaultIncludeDelivered: false,
            defaultOverdueOnly: false,
            defaultStatusFilter: OrdersBoardFilterStateMapper.AnyStatus,
            maxItems: 900,
            includeDelivered: null,
            overdueOnly: null,
            status: "not-a-status",
            dueOnOrBefore: "bad-date");

        Assert.Equal("http://localhost:5064", filters.ApiBaseUrl);
        Assert.Equal(80, filters.MaxItems);
        Assert.False(filters.IncludeDelivered);
        Assert.False(filters.OverdueOnly);
        Assert.Equal(OrdersBoardFilterStateMapper.AnyStatus, filters.StatusFilter);
        Assert.Null(filters.DueOnOrBeforeDate);
    }

    [Fact]
    public void ToQueryParameters_WithDueDate_FormatsStableValues()
    {
        var filters = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 40,
            IncludeDelivered: true,
            OverdueOnly: false,
            StatusFilter: "Ready",
            DueOnOrBeforeDate: new DateTime(2026, 5, 10));

        var query = OrdersBoardFilterStateMapper.ToQueryParameters(filters);

        Assert.Equal(40, query["maxItems"]);
        Assert.Equal(true, query["includeDelivered"]);
        Assert.Equal(false, query["overdueOnly"]);
        Assert.Equal("Ready", query["status"]);
        Assert.Equal("2026-05-10", query["dueOnOrBefore"]);
    }

    [Fact]
    public void ToDueOnOrBeforeUtc_WithDate_ReturnsUtcDayEnd()
    {
        var dueDate = new DateTime(2026, 5, 10);

        var cutoff = OrdersBoardFilterStateMapper.ToDueOnOrBeforeUtc(dueDate);

        Assert.NotNull(cutoff);
        Assert.Equal(
            new DateTimeOffset(dueDate.AddDays(1).AddTicks(-1), TimeSpan.Zero),
            cutoff!.Value);
    }

    [Fact]
    public void ApplyPreset_TodayDue_SetsDateAndClearsConflictingFlags()
    {
        var current = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 50,
            IncludeDelivered: true,
            OverdueOnly: true,
            StatusFilter: "Ready",
            DueOnOrBeforeDate: null);

        var updated = OrdersBoardFilterStateMapper.ApplyPreset(
            current,
            OrdersBoardQuickPreset.TodayDue,
            new DateTime(2026, 5, 11, 15, 30, 0, DateTimeKind.Utc));

        Assert.Equal("http://localhost:5064", updated.ApiBaseUrl);
        Assert.Equal(50, updated.MaxItems);
        Assert.False(updated.IncludeDelivered);
        Assert.False(updated.OverdueOnly);
        Assert.Equal(OrdersBoardFilterStateMapper.AnyStatus, updated.StatusFilter);
        Assert.Equal(new DateTime(2026, 5, 11), updated.DueOnOrBeforeDate);
    }

    [Fact]
    public void ApplyPreset_Ready_SetsReadyAndClearsDueAndOverdue()
    {
        var current = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 25,
            IncludeDelivered: true,
            OverdueOnly: true,
            StatusFilter: "InProgress",
            DueOnOrBeforeDate: new DateTime(2026, 5, 11));

        var updated = OrdersBoardFilterStateMapper.ApplyPreset(
            current,
            OrdersBoardQuickPreset.Ready,
            new DateTime(2026, 5, 11));

        Assert.False(updated.IncludeDelivered);
        Assert.False(updated.OverdueOnly);
        Assert.Equal("Ready", updated.StatusFilter);
        Assert.Null(updated.DueOnOrBeforeDate);
    }

    [Fact]
    public void ApplyPreset_Late_SetsOverdueOnlyAndClearsDueAndStatus()
    {
        var current = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 70,
            IncludeDelivered: true,
            OverdueOnly: false,
            StatusFilter: "TrialFitting",
            DueOnOrBeforeDate: new DateTime(2026, 5, 11));

        var updated = OrdersBoardFilterStateMapper.ApplyPreset(
            current,
            OrdersBoardQuickPreset.Late,
            new DateTime(2026, 5, 11));

        Assert.False(updated.IncludeDelivered);
        Assert.True(updated.OverdueOnly);
        Assert.Equal(OrdersBoardFilterStateMapper.AnyStatus, updated.StatusFilter);
        Assert.Null(updated.DueOnOrBeforeDate);
    }

    [Fact]
    public void MatchesPreset_WithMatchingTodayDueFilters_ReturnsTrue()
    {
        var nowUtc = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);
        var current = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 80,
            IncludeDelivered: false,
            OverdueOnly: false,
            StatusFilter: OrdersBoardFilterStateMapper.AnyStatus,
            DueOnOrBeforeDate: nowUtc.Date);

        var isMatch = OrdersBoardFilterStateMapper.MatchesPreset(
            current,
            OrdersBoardQuickPreset.TodayDue,
            nowUtc);

        Assert.True(isMatch);
    }

    [Fact]
    public void MatchesPreset_WithDifferentFilters_ReturnsFalse()
    {
        var nowUtc = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);
        var current = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 80,
            IncludeDelivered: false,
            OverdueOnly: false,
            StatusFilter: "Ready",
            DueOnOrBeforeDate: null);

        var isMatch = OrdersBoardFilterStateMapper.MatchesPreset(
            current,
            OrdersBoardQuickPreset.Late,
            nowUtc);

        Assert.False(isMatch);
    }

    [Fact]
    public void ToQueryParameters_WithDefaultsAndMatchingValues_EmitsNullsForDefaultKeys()
    {
        var defaults = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 80,
            IncludeDelivered: false,
            OverdueOnly: false,
            StatusFilter: OrdersBoardFilterStateMapper.AnyStatus,
            DueOnOrBeforeDate: null);

        var query = OrdersBoardFilterStateMapper.ToQueryParameters(defaults, defaults);

        Assert.Null(query["maxItems"]);
        Assert.Null(query["includeDelivered"]);
        Assert.Null(query["overdueOnly"]);
        Assert.Null(query["status"]);
        Assert.Null(query["dueOnOrBefore"]);
    }

    [Fact]
    public void ToQueryParameters_WithDefaultsAndDifferences_EmitsOnlyChangedValues()
    {
        var defaults = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 80,
            IncludeDelivered: false,
            OverdueOnly: false,
            StatusFilter: OrdersBoardFilterStateMapper.AnyStatus,
            DueOnOrBeforeDate: null);

        var filters = new OrdersBoardFilterState(
            ApiBaseUrl: "http://localhost:5064",
            MaxItems: 25,
            IncludeDelivered: true,
            OverdueOnly: true,
            StatusFilter: "Ready",
            DueOnOrBeforeDate: new DateTime(2026, 5, 15));

        var query = OrdersBoardFilterStateMapper.ToQueryParameters(filters, defaults);

        Assert.Equal(25, query["maxItems"]);
        Assert.Equal(true, query["includeDelivered"]);
        Assert.Equal(true, query["overdueOnly"]);
        Assert.Equal("Ready", query["status"]);
        Assert.Equal("2026-05-15", query["dueOnOrBefore"]);
    }

    [Fact]
    public void BuildQueryFingerprint_WithEquivalentInputs_ReturnsSameValue()
    {
        var first = OrdersBoardFilterStateMapper.BuildQueryFingerprint(
            maxItems: 80,
            includeDelivered: false,
            overdueOnly: false,
            status: "ready",
            dueOnOrBefore: "2026-05-15");

        var second = OrdersBoardFilterStateMapper.BuildQueryFingerprint(
            maxItems: 80,
            includeDelivered: false,
            overdueOnly: false,
            status: "Ready",
            dueOnOrBefore: "2026-05-15");

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildQueryFingerprint_WithDifferentEffectiveInputs_ReturnsDifferentValues()
    {
        var first = OrdersBoardFilterStateMapper.BuildQueryFingerprint(
            maxItems: 80,
            includeDelivered: false,
            overdueOnly: false,
            status: OrdersBoardFilterStateMapper.AnyStatus,
            dueOnOrBefore: null);

        var second = OrdersBoardFilterStateMapper.BuildQueryFingerprint(
            maxItems: 80,
            includeDelivered: false,
            overdueOnly: true,
            status: OrdersBoardFilterStateMapper.AnyStatus,
            dueOnOrBefore: null);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("In Progress", "InProgress")]
    [InlineData("in-progress", "InProgress")]
    [InlineData("Trial/Fitting", "TrialFitting")]
    [InlineData("trial fitting", "TrialFitting")]
    [InlineData("ALL", "Any")]
    [InlineData("any", "Any")]
    public void NormalizeStatus_WithAliases_ReturnsCanonicalValue(string input, string expected)
    {
        var normalized = OrdersBoardFilterStateMapper.NormalizeStatus(input);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("TodayDue", OrdersBoardQuickPreset.TodayDue)]
    [InlineData("today", OrdersBoardQuickPreset.TodayDue)]
    [InlineData("Today Due", OrdersBoardQuickPreset.TodayDue)]
    [InlineData("Ready", OrdersBoardQuickPreset.Ready)]
    [InlineData("ReadyOnly", OrdersBoardQuickPreset.Ready)]
    [InlineData("ready", OrdersBoardQuickPreset.Ready)]
    [InlineData("ready-only", OrdersBoardQuickPreset.Ready)]
    [InlineData("late", OrdersBoardQuickPreset.Late)]
    [InlineData("overdue", OrdersBoardQuickPreset.Late)]
    [InlineData("Due This Week", OrdersBoardQuickPreset.DueThisWeek)]
    [InlineData("unpaid", OrdersBoardQuickPreset.Unpaid)]
    [InlineData("delivered", OrdersBoardQuickPreset.Delivered)]
    public void ParsePreset_WithAliases_ReturnsExpectedPreset(string input, OrdersBoardQuickPreset expected)
    {
        var parsed = OrdersBoardFilterStateMapper.ParsePreset(input);

        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ParsePreset_WithInvalidValue_ReturnsNull()
    {
        var parsed = OrdersBoardFilterStateMapper.ParsePreset("not-a-preset");

        Assert.Null(parsed);
    }

    [Fact]
    public void ToPresetQueryValue_WithPreset_ReturnsCanonicalPresetName()
    {
        var queryValue = OrdersBoardFilterStateMapper.ToPresetQueryValue(OrdersBoardQuickPreset.Ready);

        Assert.Equal("Ready", queryValue);
    }

    [Fact]
    public void ParsePreset_WithAlias_RoundTripsToCanonicalQueryValue()
    {
        var parsed = OrdersBoardFilterStateMapper.ParsePreset("today due");

        var queryValue = OrdersBoardFilterStateMapper.ToPresetQueryValue(parsed);

        Assert.Equal("TodayDue", queryValue);
    }

    [Fact]
    public void ToPresetQueryValue_WithNullPreset_ReturnsNull()
    {
        var queryValue = OrdersBoardFilterStateMapper.ToPresetQueryValue(null);

        Assert.Null(queryValue);
    }
}
