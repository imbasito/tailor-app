using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardWorklistOrganizerTests
{
    [Fact]
    public void BuildGroups_WithMixedItems_ReturnsWorkflowGroupOrder()
    {
        var nowUtc = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var overdue = CreateItem("InProgress", dueDateUtc: new DateTime(2026, 5, 19), balance: 200m, customerName: "Samir");
        var dueToday = CreateItem("TrialFitting", dueDateUtc: new DateTime(2026, 5, 20), balance: 120m, customerName: "Amina");
        var readyWithBalance = CreateItem("Ready", dueDateUtc: new DateTime(2026, 5, 23), balance: 80m, customerName: "Noor");
        var upcoming = CreateItem("New", dueDateUtc: new DateTime(2026, 5, 24), balance: 50m, customerName: "Rahma");
        var delivered = CreateItem("Delivered", dueDateUtc: new DateTime(2026, 5, 18), balance: 0m, customerName: "Khalid");

        var groups = OrdersBoardWorklistOrganizer.BuildGroups(
            [upcoming, delivered, readyWithBalance, dueToday, overdue],
            nowUtc);

        Assert.Equal(5, groups.Count);
        Assert.Equal(OrdersBoardWorklistGroupKind.New, groups[0].Kind);
        Assert.Equal(OrdersBoardWorklistGroupKind.InProgress, groups[1].Kind);
        Assert.Equal(OrdersBoardWorklistGroupKind.TrialFitting, groups[2].Kind);
        Assert.Equal(OrdersBoardWorklistGroupKind.Ready, groups[3].Kind);
        Assert.Equal(OrdersBoardWorklistGroupKind.Delivered, groups[4].Kind);

        Assert.Single(groups[0].Items);
        Assert.Equal(upcoming.OrderId, groups[0].Items[0].OrderId);
    }

    [Fact]
    public void BuildGroups_WithReadyAliasAndPositiveBalance_UsesReadyBucket()
    {
        var nowUtc = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var item = CreateItem("ready", dueDateUtc: new DateTime(2026, 5, 22), balance: 150m, customerName: "Amina");

        var groups = OrdersBoardWorklistOrganizer.BuildGroups([item], nowUtc);

        var group = Assert.Single(groups);
        Assert.Equal(OrdersBoardWorklistGroupKind.Ready, group.Kind);
        Assert.Equal("Ready", group.Title);
    }

    [Fact]
    public void BuildGroups_WithSameGroupItems_SortsByDueDateThenBalanceThenCustomer()
    {
        var nowUtc = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var first = CreateItem("InProgress", dueDateUtc: new DateTime(2026, 5, 19), balance: 100m, customerName: "Zara");
        var second = CreateItem("InProgress", dueDateUtc: new DateTime(2026, 5, 19), balance: 180m, customerName: "Amina");
        var third = CreateItem("InProgress", dueDateUtc: new DateTime(2026, 5, 18), balance: 20m, customerName: "Khalid");

        var groups = OrdersBoardWorklistOrganizer.BuildGroups([first, second, third], nowUtc);

        var inProgress = Assert.Single(groups);
        Assert.Equal(OrdersBoardWorklistGroupKind.InProgress, inProgress.Kind);
        Assert.Equal(3, inProgress.Items.Count);
        Assert.Equal(third.OrderId, inProgress.Items[0].OrderId);
        Assert.Equal(second.OrderId, inProgress.Items[1].OrderId);
        Assert.Equal(first.OrderId, inProgress.Items[2].OrderId);
    }

    [Fact]
    public void BuildGroups_WithEmptyItems_ReturnsEmptyList()
    {
        var groups = OrdersBoardWorklistOrganizer.BuildGroups([], new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc));

        Assert.Empty(groups);
    }

    private static OrderWorklistItemDto CreateItem(
        string status,
        DateTime dueDateUtc,
        decimal balance,
        string customerName)
    {
        var charged = 300m;
        var paid = charged - balance;

        return new OrderWorklistItemDto(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerName: customerName,
            PhoneNumber: "+251900000001",
            City: "Harar",
            GarmentType: "Suit",
            Status: status,
            AmountCharged: charged,
            AmountPaid: paid,
            BalanceDue: balance,
            ReceivedAtUtc: new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
            DueAtUtc: new DateTimeOffset(dueDateUtc.Year, dueDateUtc.Month, dueDateUtc.Day, 0, 0, 0, TimeSpan.Zero));
    }
}
