using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardGroupUiStateTests
{
    [Fact]
    public void Toggle_WhenGroupExpanded_CollapsesIt()
    {
        var state = new OrdersBoardGroupUiState();

        var collapsed = state.Toggle(OrdersBoardWorklistGroupKind.New);

        Assert.True(collapsed);
        Assert.True(state.IsCollapsed(OrdersBoardWorklistGroupKind.New));
    }

    [Fact]
    public void Toggle_WhenGroupCollapsed_ExpandsIt()
    {
        var state = new OrdersBoardGroupUiState();
        state.Toggle(OrdersBoardWorklistGroupKind.New);

        var collapsed = state.Toggle(OrdersBoardWorklistGroupKind.New);

        Assert.False(collapsed);
        Assert.False(state.IsCollapsed(OrdersBoardWorklistGroupKind.New));
    }

    [Fact]
    public void SyncToGroups_RemovesCollapsedKindsThatNoLongerExist()
    {
        var state = new OrdersBoardGroupUiState();
        state.Toggle(OrdersBoardWorklistGroupKind.New);
        state.Toggle(OrdersBoardWorklistGroupKind.Delivered);

        state.SyncToGroups(
        [
            new OrdersBoardWorklistGroup(
                OrdersBoardWorklistGroupKind.New,
                "New",
                []),
        ]);

        Assert.True(state.IsCollapsed(OrdersBoardWorklistGroupKind.New));
        Assert.False(state.IsCollapsed(OrdersBoardWorklistGroupKind.Delivered));
    }

    [Fact]
    public void ExpandAll_ClearsCollapsedState()
    {
        var state = new OrdersBoardGroupUiState();
        state.Toggle(OrdersBoardWorklistGroupKind.New);
        state.Toggle(OrdersBoardWorklistGroupKind.TrialFitting);

        state.ExpandAll();

        Assert.False(state.IsCollapsed(OrdersBoardWorklistGroupKind.New));
        Assert.False(state.IsCollapsed(OrdersBoardWorklistGroupKind.TrialFitting));
    }

    [Fact]
    public void ParseCollapsedGroupsQuery_WithMixedTokens_ReturnsDistinctOrderedKinds()
    {
        var kinds = OrdersBoardGroupUiState.ParseCollapsedGroupsQuery("ready,progress,unknown,fitting,ready");

        Assert.Equal(3, kinds.Count);
        Assert.Equal(OrdersBoardWorklistGroupKind.InProgress, kinds[0]);
        Assert.Equal(OrdersBoardWorklistGroupKind.TrialFitting, kinds[1]);
        Assert.Equal(OrdersBoardWorklistGroupKind.Ready, kinds[2]);
    }

    [Fact]
    public void BuildCollapsedGroupsQueryValue_WithKinds_ReturnsCanonicalCsv()
    {
        var query = OrdersBoardGroupUiState.BuildCollapsedGroupsQueryValue(
        [
            OrdersBoardWorklistGroupKind.Ready,
            OrdersBoardWorklistGroupKind.New,
            OrdersBoardWorklistGroupKind.TrialFitting,
        ]);

        Assert.Equal("New,TrialFitting,Ready", query);
    }

    [Fact]
    public void SetCollapsedKinds_UpdatesCurrentCollapsedSet()
    {
        var state = new OrdersBoardGroupUiState();

        state.SetCollapsedKinds(
        [
            OrdersBoardWorklistGroupKind.Delivered,
            OrdersBoardWorklistGroupKind.InProgress,
        ]);

        var kinds = state.GetCollapsedKinds();
        Assert.Equal(2, kinds.Count);
        Assert.Equal(OrdersBoardWorklistGroupKind.InProgress, kinds[0]);
        Assert.Equal(OrdersBoardWorklistGroupKind.Delivered, kinds[1]);
    }
}
