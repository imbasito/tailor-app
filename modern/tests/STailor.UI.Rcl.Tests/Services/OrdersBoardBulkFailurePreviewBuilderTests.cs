using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardBulkFailurePreviewBuilderTests
{
    [Fact]
    public void Build_WithNoCandidates_ReturnsEmptyPreview()
    {
        var preview = OrdersBoardBulkFailurePreviewBuilder.Build(
            failedCandidates: null,
            maxItems: 3);

        Assert.False(preview.HasItems);
        Assert.False(preview.IsTruncated);
        Assert.Empty(preview.Names);
        Assert.Equal(0, preview.HiddenCount);
    }

    [Fact]
    public void Build_WithDuplicateAndBlankNames_NormalizesAndDeduplicates()
    {
        var failedCandidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                CreateCandidate("  Amina  "),
                CreateCandidate("amina"),
                CreateCandidate(""),
                CreateCandidate("  "),
                CreateCandidate("Noor"),
            };

        var preview = OrdersBoardBulkFailurePreviewBuilder.Build(failedCandidates, maxItems: 5);

        Assert.True(preview.HasItems);
        Assert.False(preview.IsTruncated);
        Assert.Equal(2, preview.Names.Count);
        Assert.Equal("Amina", preview.Names[0]);
        Assert.Equal("Noor", preview.Names[1]);
        Assert.Equal(0, preview.HiddenCount);
    }

    [Fact]
    public void Build_WithMoreThanLimit_ReturnsHiddenCount()
    {
        var failedCandidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                CreateCandidate("Amina"),
                CreateCandidate("Noor"),
                CreateCandidate("Samir"),
                CreateCandidate("Rahma"),
            };

        var preview = OrdersBoardBulkFailurePreviewBuilder.Build(failedCandidates, maxItems: 2);

        Assert.True(preview.HasItems);
        Assert.True(preview.IsTruncated);
        Assert.Equal(2, preview.Names.Count);
        Assert.Equal("Amina", preview.Names[0]);
        Assert.Equal("Noor", preview.Names[1]);
        Assert.Equal(2, preview.HiddenCount);
    }

    private static OrdersBoardBulkAdvanceRetryCandidate CreateCandidate(string customerName)
    {
        return new OrdersBoardBulkAdvanceRetryCandidate(
            Guid.NewGuid(),
            customerName,
            "Ready");
    }
}