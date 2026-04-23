using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardBulkAdvanceRetryPlannerTests
{
    [Fact]
    public void BuildSafeRetryCandidates_WithStatusAlignedCandidates_ReturnsMatchingCandidates()
    {
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();

        var failedCandidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(firstOrderId, "Amina", "InProgress"),
                new(secondOrderId, "Noor", "TrialFitting"),
            };

        var currentItems =
            new List<OrderWorklistItemDto>
            {
                CreateItem(firstOrderId, "New", "Amina Updated"),
                CreateItem(secondOrderId, "In Progress", "Noor Updated"),
            };

        var safeCandidates = OrdersBoardBulkAdvanceRetryPlanner.BuildSafeRetryCandidates(failedCandidates, currentItems);

        Assert.Equal(2, safeCandidates.Count);
        Assert.Equal(firstOrderId, safeCandidates[0].OrderId);
        Assert.Equal("Amina Updated", safeCandidates[0].CustomerName);
        Assert.Equal("InProgress", safeCandidates[0].TargetStatus);
        Assert.Equal(secondOrderId, safeCandidates[1].OrderId);
        Assert.Equal("Noor Updated", safeCandidates[1].CustomerName);
        Assert.Equal("TrialFitting", safeCandidates[1].TargetStatus);
    }

    [Fact]
    public void BuildSafeRetryCandidates_WithUnorderedCandidates_SortsByTargetThenCustomerName()
    {
        var readyZaraOrderId = Guid.NewGuid();
        var inProgressNoorOrderId = Guid.NewGuid();
        var inProgressAminaOrderId = Guid.NewGuid();
        var trialBilalOrderId = Guid.NewGuid();

        var failedCandidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(readyZaraOrderId, "Zara", "Ready"),
                new(inProgressNoorOrderId, "Noor", "InProgress"),
                new(inProgressAminaOrderId, "Amina", "InProgress"),
                new(trialBilalOrderId, "Bilal", "TrialFitting"),
            };

        var currentItems =
            new List<OrderWorklistItemDto>
            {
                CreateItem(readyZaraOrderId, "Rework", "Zara"),
                CreateItem(inProgressNoorOrderId, "New", "Noor"),
                CreateItem(inProgressAminaOrderId, "New", "Amina"),
                CreateItem(trialBilalOrderId, "InProgress", "Bilal"),
            };

        var safeCandidates = OrdersBoardBulkAdvanceRetryPlanner.BuildSafeRetryCandidates(failedCandidates, currentItems);

        Assert.Equal(4, safeCandidates.Count);
        Assert.Equal(inProgressAminaOrderId, safeCandidates[0].OrderId);
        Assert.Equal("InProgress", safeCandidates[0].TargetStatus);
        Assert.Equal(inProgressNoorOrderId, safeCandidates[1].OrderId);
        Assert.Equal("InProgress", safeCandidates[1].TargetStatus);
        Assert.Equal(trialBilalOrderId, safeCandidates[2].OrderId);
        Assert.Equal("TrialFitting", safeCandidates[2].TargetStatus);
        Assert.Equal(readyZaraOrderId, safeCandidates[3].OrderId);
        Assert.Equal("Ready", safeCandidates[3].TargetStatus);
    }

    [Fact]
    public void BuildSafeRetryCandidates_WithMissingOrDriftedStatus_ExcludesUnsafeCandidates()
    {
        var keptOrderId = Guid.NewGuid();
        var missingOrderId = Guid.NewGuid();
        var driftedOrderId = Guid.NewGuid();
        var deliveredOrderId = Guid.NewGuid();

        var failedCandidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(keptOrderId, "Amina", "InProgress"),
                new(missingOrderId, "Noor", "TrialFitting"),
                new(driftedOrderId, "Samir", "TrialFitting"),
                new(deliveredOrderId, "Rahma", "Delivered"),
            };

        var currentItems =
            new List<OrderWorklistItemDto>
            {
                CreateItem(keptOrderId, "new", "Amina"),
                CreateItem(driftedOrderId, "Rework", "Samir"),
                CreateItem(deliveredOrderId, "Delivered", "Rahma"),
            };

        var safeCandidates = OrdersBoardBulkAdvanceRetryPlanner.BuildSafeRetryCandidates(failedCandidates, currentItems);

        var keptCandidate = Assert.Single(safeCandidates);
        Assert.Equal(keptOrderId, keptCandidate.OrderId);
        Assert.Equal("InProgress", keptCandidate.TargetStatus);
    }

    [Fact]
    public void BuildSafeRetryCandidates_WithNullFailedCandidates_ReturnsEmpty()
    {
        var safeCandidates = OrdersBoardBulkAdvanceRetryPlanner.BuildSafeRetryCandidates(
            failedCandidates: null,
            currentItems: []);

        Assert.Empty(safeCandidates);
    }

    [Fact]
    public void BuildPreview_WithMoreCandidatesThanLimit_TruncatesAndTracksHiddenCount()
    {
        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(Guid.NewGuid(), "Amina", "InProgress"),
                new(Guid.NewGuid(), "Noor", "TrialFitting"),
                new(Guid.NewGuid(), "Samir", "Rework"),
                new(Guid.NewGuid(), "Rahma", "Ready"),
            };

        var preview = OrdersBoardBulkAdvanceRetryPlanner.BuildPreview(candidates, maxItems: 2);

        Assert.Equal(2, preview.Items.Count);
        Assert.True(preview.IsTruncated);
        Assert.Equal(2, preview.HiddenCount);
        Assert.Equal("Amina", preview.Items[0].CustomerName);
        Assert.Equal("InProgress", preview.Items[0].TargetStatus);
        Assert.Equal("Noor", preview.Items[1].CustomerName);
        Assert.Equal("TrialFitting", preview.Items[1].TargetStatus);
    }

    [Fact]
    public void BuildTargetDistribution_WithMixedTargets_AggregatesAndOrdersByWorkflow()
    {
        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(Guid.NewGuid(), "Amina", "TrialFitting"),
                new(Guid.NewGuid(), "Noor", "InProgress"),
                new(Guid.NewGuid(), "Samir", "Ready"),
                new(Guid.NewGuid(), "Rahma", "InProgress"),
            };

        var distribution = OrdersBoardBulkAdvanceRetryPlanner.BuildTargetDistribution(candidates);

        Assert.Equal(3, distribution.Count);
        Assert.Equal("InProgress", distribution[0].TargetStatus);
        Assert.Equal(2, distribution[0].Count);
        Assert.Equal("TrialFitting", distribution[1].TargetStatus);
        Assert.Equal(1, distribution[1].Count);
        Assert.Equal("Ready", distribution[2].TargetStatus);
        Assert.Equal(1, distribution[2].Count);
    }

    private static OrderWorklistItemDto CreateItem(Guid orderId, string status, string customerName)
    {
        return new OrderWorklistItemDto(
            OrderId: orderId,
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