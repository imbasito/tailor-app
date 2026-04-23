using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class OrdersBoardBulkAdvanceResultBuilderTests
{
    [Fact]
    public void Build_WithAllSuccessful_ReturnsSuccessOutcomeAndMessage()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Overdue",
            successCount: 3,
            failedCount: 0);

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Success, summary.Outcome);
        Assert.Equal("Advanced 3 order(s) in Overdue.", summary.Message);
        Assert.Equal(3, summary.SuccessCount);
        Assert.Equal(0, summary.FailedCount);
    }

    [Fact]
    public void Build_WithPartialFailures_ReturnsPartialOutcomeAndIncludesSample()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Upcoming",
            successCount: 2,
            failedCount: 1,
            sampleFailedCustomerName: "Amina");

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Partial, summary.Outcome);
        Assert.Equal("Advanced 2 order(s) in Upcoming; 1 failed (e.g. Amina).", summary.Message);
        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(1, summary.FailedCount);
    }

    [Fact]
    public void Build_WithAllFailures_ReturnsFailureOutcomeAndMessage()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Ready With Balance",
            successCount: 0,
            failedCount: 4,
            sampleFailedCustomerName: "Noor");

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Failure, summary.Outcome);
        Assert.Equal("Unable to advance orders in Ready With Balance.", summary.Message);
        Assert.Equal(0, summary.SuccessCount);
        Assert.Equal(4, summary.FailedCount);
    }

    [Fact]
    public void Build_WithRetrySuccess_UsesRetryMessageWording()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Overdue",
            successCount: 2,
            failedCount: 0,
            actionType: OrdersBoardBulkActionType.Retry);

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Success, summary.Outcome);
        Assert.Equal("Retried 2 order(s) in Overdue.", summary.Message);
    }

    [Fact]
    public void Build_WithRetryPartialFailure_UsesRetryMessageWording()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Upcoming",
            successCount: 1,
            failedCount: 1,
            sampleFailedCustomerName: "Amina",
            actionType: OrdersBoardBulkActionType.Retry);

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Partial, summary.Outcome);
        Assert.Equal("Retried 1 order(s) in Upcoming; 1 failed (e.g. Amina).", summary.Message);
    }

    [Fact]
    public void Build_WithRetryAllFailures_UsesRetryFailureMessageWording()
    {
        var summary = OrdersBoardBulkAdvanceResultBuilder.Build(
            groupTitle: "Due Today",
            successCount: 0,
            failedCount: 3,
            actionType: OrdersBoardBulkActionType.Retry);

        Assert.Equal(OrdersBoardBulkAdvanceOutcome.Failure, summary.Outcome);
        Assert.Equal("Unable to retry orders in Due Today.", summary.Message);
    }
}