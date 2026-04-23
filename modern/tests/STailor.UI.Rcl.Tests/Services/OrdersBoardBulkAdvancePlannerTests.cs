using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardBulkAdvancePlannerTests
{
    [Fact]
    public void BuildPlan_WithMixedStatuses_IncludesEligibleItemsInInputOrder()
    {
        var first = CreateItem("New", "Amina");
        var second = CreateItem("Delivered", "Noor");
        var third = CreateItem("In Progress", "Samir");

        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Upcoming,
            "Upcoming",
            [first, second, third]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        Assert.Equal(OrdersBoardWorklistGroupKind.Upcoming, plan.GroupKind);
        Assert.Equal("Upcoming", plan.GroupTitle);
        Assert.True(plan.HasEligible);
        Assert.Equal(2, plan.EligibleCount);
        Assert.Equal(first.OrderId, plan.Candidates[0].OrderId);
        Assert.Equal("InProgress", plan.Candidates[0].TargetStatus);
        Assert.Equal(third.OrderId, plan.Candidates[1].OrderId);
        Assert.Equal("TrialFitting", plan.Candidates[1].TargetStatus);
    }

    [Fact]
    public void BuildPlan_WithNoAdvanceableStatuses_ReturnsEmptyPlan()
    {
        var delivered = CreateItem("Delivered", "Amina");
        var unknown = CreateItem("Archived", "Samir");

        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Delivered,
            "Delivered",
            [delivered, unknown]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        Assert.False(plan.HasEligible);
        Assert.Empty(plan.Candidates);
    }

    [Theory]
    [InlineData("new", "InProgress")]
    [InlineData("In Progress", "TrialFitting")]
    [InlineData("Trial/Fitting", "Rework")]
    [InlineData("Rework", "Ready")]
    [InlineData("READY", "Delivered")]
    public void TryGetNextStatus_WithAliases_ReturnsExpectedNextStatus(string currentStatus, string expected)
    {
        var success = OrdersBoardBulkAdvancePlanner.TryGetNextStatus(currentStatus, out var nextStatus);

        Assert.True(success);
        Assert.Equal(expected, nextStatus);
    }

    [Fact]
    public void TryGetNextStatus_WithFinalStatus_ReturnsFalse()
    {
        var success = OrdersBoardBulkAdvancePlanner.TryGetNextStatus("Delivered", out var nextStatus);

        Assert.False(success);
        Assert.Null(nextStatus);
    }

    [Fact]
    public void BuildPreview_WithMoreEligibleThanLimit_TruncatesAndTracksHiddenCount()
    {
        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Upcoming,
            "Upcoming",
            [
                CreateItem("New", "Amina"),
                CreateItem("InProgress", "Noor"),
                CreateItem("TrialFitting", "Samir"),
                CreateItem("Rework", "Rahma"),
            ]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        var preview = OrdersBoardBulkAdvancePlanner.BuildPreview(plan, maxItems: 2);

        Assert.Equal(2, preview.Items.Count);
        Assert.True(preview.IsTruncated);
        Assert.Equal(2, preview.HiddenCount);
        Assert.Equal("Amina", preview.Items[0].CustomerName);
        Assert.Equal("InProgress", preview.Items[0].TargetStatus);
        Assert.Equal("Noor", preview.Items[1].CustomerName);
        Assert.Equal("TrialFitting", preview.Items[1].TargetStatus);
    }

    [Fact]
    public void BuildPreview_WithEligibleWithinLimit_ReturnsAllWithoutHiddenCount()
    {
        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Upcoming,
            "Upcoming",
            [
                CreateItem("New", "Amina"),
                CreateItem("Delivered", "Noor"),
                CreateItem("Rework", "Samir"),
            ]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        var preview = OrdersBoardBulkAdvancePlanner.BuildPreview(plan, maxItems: 5);

        Assert.Equal(2, preview.Items.Count);
        Assert.False(preview.IsTruncated);
        Assert.Equal(0, preview.HiddenCount);
    }

    [Fact]
    public void BuildTargetDistribution_WithMixedEligibleTargets_AggregatesAndOrdersByWorkflow()
    {
        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Upcoming,
            "Upcoming",
            [
                CreateItem("New", "Amina"),
                CreateItem("new", "Noor"),
                CreateItem("InProgress", "Samir"),
                CreateItem("TrialFitting", "Rahma"),
                CreateItem("Rework", "Khalid"),
                CreateItem("Ready", "Muna"),
                CreateItem("Delivered", "Safa"),
            ]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        var distribution = OrdersBoardBulkAdvancePlanner.BuildTargetDistribution(plan);

        Assert.Equal(5, distribution.Count);
        Assert.Equal("InProgress", distribution[0].TargetStatus);
        Assert.Equal(2, distribution[0].Count);
        Assert.Equal("TrialFitting", distribution[1].TargetStatus);
        Assert.Equal(1, distribution[1].Count);
        Assert.Equal("Rework", distribution[2].TargetStatus);
        Assert.Equal(1, distribution[2].Count);
        Assert.Equal("Ready", distribution[3].TargetStatus);
        Assert.Equal(1, distribution[3].Count);
        Assert.Equal("Delivered", distribution[4].TargetStatus);
        Assert.Equal(1, distribution[4].Count);
    }

    [Fact]
    public void BuildTargetDistribution_WithNoEligibleCandidates_ReturnsEmpty()
    {
        var group = new OrdersBoardWorklistGroup(
            OrdersBoardWorklistGroupKind.Delivered,
            "Delivered",
            [
                CreateItem("Delivered", "Amina"),
                CreateItem("Archived", "Noor"),
            ]);

        var plan = OrdersBoardBulkAdvancePlanner.BuildPlan(group);

        var distribution = OrdersBoardBulkAdvancePlanner.BuildTargetDistribution(plan);

        Assert.Empty(distribution);
    }

    private static OrderWorklistItemDto CreateItem(string status, string customerName)
    {
        return new OrderWorklistItemDto(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerName: customerName,
            PhoneNumber: "+251900000001",
            City: "Harar",
            GarmentType: "Suit",
            Status: status,
            AmountCharged: 300m,
            AmountPaid: 100m,
            BalanceDue: 200m,
            ReceivedAtUtc: new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
            DueAtUtc: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero));
    }
}