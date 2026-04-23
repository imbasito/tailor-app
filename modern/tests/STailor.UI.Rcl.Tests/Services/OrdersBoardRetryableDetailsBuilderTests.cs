using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardRetryableDetailsBuilderTests
{
    [Fact]
    public void Build_WithNullCandidates_ReturnsEmptyPreview()
    {
        var preview = OrdersBoardRetryableDetailsBuilder.Build(candidates: null);

        Assert.False(preview.HasItems);
        Assert.False(preview.IsTruncated);
        Assert.Empty(preview.Items);
        Assert.Equal(0, preview.HiddenCount);
    }

    [Fact]
    public void Build_WithBlankName_NormalizesToUnknownCustomer()
    {
        var orderId = Guid.NewGuid();
        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(orderId, "  ", "InProgress"),
            };

        var preview = OrdersBoardRetryableDetailsBuilder.Build(candidates, maxItems: 5);

        var item = Assert.Single(preview.Items);
        Assert.Equal(orderId, item.OrderId);
        Assert.Equal("Unknown Customer", item.CustomerName);
        Assert.Equal("InProgress", item.TargetStatus);
        Assert.False(preview.IsTruncated);
        Assert.Equal(0, preview.HiddenCount);
    }

    [Fact]
    public void Build_WithUnorderedCandidates_SortsByTargetThenCustomerName()
    {
        var readyOrderId = Guid.NewGuid();
        var inProgressNoorOrderId = Guid.NewGuid();
        var inProgressAminaOrderId = Guid.NewGuid();
        var trialBilalOrderId = Guid.NewGuid();

        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(readyOrderId, "Zara", "Ready"),
                new(inProgressNoorOrderId, "Noor", "InProgress"),
                new(inProgressAminaOrderId, "Amina", "InProgress"),
                new(trialBilalOrderId, "Bilal", "TrialFitting"),
            };

        var preview = OrdersBoardRetryableDetailsBuilder.Build(candidates, maxItems: 10);

        Assert.Equal(4, preview.Items.Count);
        Assert.Equal(inProgressAminaOrderId, preview.Items[0].OrderId);
        Assert.Equal("Amina", preview.Items[0].CustomerName);
        Assert.Equal("InProgress", preview.Items[0].TargetStatus);
        Assert.Equal(inProgressNoorOrderId, preview.Items[1].OrderId);
        Assert.Equal("Noor", preview.Items[1].CustomerName);
        Assert.Equal("InProgress", preview.Items[1].TargetStatus);
        Assert.Equal(trialBilalOrderId, preview.Items[2].OrderId);
        Assert.Equal("Bilal", preview.Items[2].CustomerName);
        Assert.Equal("TrialFitting", preview.Items[2].TargetStatus);
        Assert.Equal(readyOrderId, preview.Items[3].OrderId);
        Assert.Equal("Zara", preview.Items[3].CustomerName);
        Assert.Equal("Ready", preview.Items[3].TargetStatus);
    }

    [Fact]
    public void Build_WithMoreThanLimit_ReturnsTruncatedPreview()
    {
        var aminaOrderId = Guid.NewGuid();
        var noorOrderId = Guid.NewGuid();
        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(aminaOrderId, "Amina", "InProgress"),
                new(noorOrderId, "Noor", "TrialFitting"),
                new(Guid.NewGuid(), "Samir", "Rework"),
            };

        var preview = OrdersBoardRetryableDetailsBuilder.Build(candidates, maxItems: 2);

        Assert.Equal(2, preview.Items.Count);
        Assert.True(preview.IsTruncated);
        Assert.Equal(1, preview.HiddenCount);
        Assert.Equal(aminaOrderId, preview.Items[0].OrderId);
        Assert.Equal("Amina", preview.Items[0].CustomerName);
        Assert.Equal("InProgress", preview.Items[0].TargetStatus);
        Assert.Equal(noorOrderId, preview.Items[1].OrderId);
        Assert.Equal("Noor", preview.Items[1].CustomerName);
        Assert.Equal("TrialFitting", preview.Items[1].TargetStatus);
    }
}