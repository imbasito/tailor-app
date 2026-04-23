using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardBulkResultMetaBuilderTests
{
    [Fact]
    public void ResolveLastFailedAtUtc_WithFailures_UsesOccurredAtUtc()
    {
        var previous = new DateTimeOffset(2026, 4, 18, 9, 0, 0, TimeSpan.Zero);
        var occurred = new DateTimeOffset(2026, 4, 19, 11, 0, 0, TimeSpan.Zero);

        var resolved = OrdersBoardBulkResultMetaBuilder.ResolveLastFailedAtUtc(
            previous,
            failedCount: 2,
            occurred);

        Assert.Equal(occurred, resolved);
    }

    [Fact]
    public void ResolveLastFailedAtUtc_WithoutFailures_PreservesPreviousValue()
    {
        var previous = new DateTimeOffset(2026, 4, 18, 9, 0, 0, TimeSpan.Zero);
        var occurred = new DateTimeOffset(2026, 4, 19, 11, 0, 0, TimeSpan.Zero);

        var resolved = OrdersBoardBulkResultMetaBuilder.ResolveLastFailedAtUtc(
            previous,
            failedCount: 0,
            occurred);

        Assert.Equal(previous, resolved);
    }

    [Fact]
    public void BuildRetryableNowLabel_ReturnsExpectedLabel()
    {
        var label = OrdersBoardBulkResultMetaBuilder.BuildRetryableNowLabel(3);

        Assert.Equal("Retryable now: 3", label);
    }
}