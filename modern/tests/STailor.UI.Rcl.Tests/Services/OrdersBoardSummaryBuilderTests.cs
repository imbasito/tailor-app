using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardSummaryBuilderTests
{
    [Fact]
    public void Build_WithMixedItems_ComputesCountsTotalsAndUrgencyBuckets()
    {
        var nowUtc = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc);

        var items = new[]
        {
            CreateItem("New", charged: 100m, paid: 20m, balance: 80m, dueDateUtc: new DateTime(2026, 5, 19)),
            CreateItem("InProgress", charged: 200m, paid: 100m, balance: 100m, dueDateUtc: new DateTime(2026, 5, 20)),
            CreateItem("Ready", charged: 300m, paid: 150m, balance: 150m, dueDateUtc: new DateTime(2026, 5, 21)),
            CreateItem("Delivered", charged: 150m, paid: 150m, balance: 0m, dueDateUtc: new DateTime(2026, 5, 18)),
        };

        var summary = OrdersBoardSummaryBuilder.Build(items, nowUtc);

        Assert.Equal(4, summary.TotalOrders);
        Assert.Equal(1, summary.NewCount);
        Assert.Equal(1, summary.InProgressCount);
        Assert.Equal(0, summary.TrialFittingCount);
        Assert.Equal(0, summary.ReworkCount);
        Assert.Equal(1, summary.ReadyCount);
        Assert.Equal(1, summary.DeliveredCount);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(1, summary.DueTodayCount);
        Assert.Equal(2, summary.AtRiskCount);
        Assert.Equal(1, summary.ReadyWithBalanceCount);
        Assert.Equal(750m, summary.TotalCharged);
        Assert.Equal(420m, summary.TotalPaid);
        Assert.Equal(330m, summary.TotalBalanceDue);
        Assert.Equal(56m, summary.CollectionRatePercent);
    }

    [Fact]
    public void Build_WithAliasStatuses_NormalizesToCanonicalBuckets()
    {
        var nowUtc = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc);

        var items = new[]
        {
            CreateItem("In Progress", charged: 120m, paid: 20m, balance: 100m, dueDateUtc: new DateTime(2026, 5, 20)),
            CreateItem("Trial/Fitting", charged: 220m, paid: 120m, balance: 100m, dueDateUtc: new DateTime(2026, 5, 21)),
        };

        var summary = OrdersBoardSummaryBuilder.Build(items, nowUtc);

        Assert.Equal(0, summary.NewCount);
        Assert.Equal(1, summary.InProgressCount);
        Assert.Equal(1, summary.TrialFittingCount);
        Assert.Equal(0, summary.ReworkCount);
        Assert.Equal(0, summary.ReadyCount);
        Assert.Equal(0, summary.DeliveredCount);
    }

    [Fact]
    public void Build_WithNoItems_ReturnsEmptySummary()
    {
        var summary = OrdersBoardSummaryBuilder.Build([], new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(OrdersBoardSummaryBuilder.Empty, summary);
    }

    [Fact]
    public void Build_WithMultipleReadyOrders_TracksOnlyReadyOrdersWithBalance()
    {
        var nowUtc = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc);

        var items = new[]
        {
            CreateItem("Ready", charged: 200m, paid: 200m, balance: 0m, dueDateUtc: new DateTime(2026, 5, 20)),
            CreateItem("Ready", charged: 180m, paid: 80m, balance: 100m, dueDateUtc: new DateTime(2026, 5, 20)),
            CreateItem("Delivered", charged: 160m, paid: 130m, balance: 30m, dueDateUtc: new DateTime(2026, 5, 19)),
        };

        var summary = OrdersBoardSummaryBuilder.Build(items, nowUtc);

        Assert.Equal(2, summary.ReadyCount);
        Assert.Equal(1, summary.ReadyWithBalanceCount);
        Assert.Equal(2, summary.AtRiskCount);
    }

    private static OrderWorklistItemDto CreateItem(
        string status,
        decimal charged,
        decimal paid,
        decimal balance,
        DateTime dueDateUtc)
    {
        return new OrderWorklistItemDto(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerName: "Amina Noor",
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
