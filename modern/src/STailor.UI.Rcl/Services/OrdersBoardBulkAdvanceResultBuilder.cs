namespace STailor.UI.Rcl.Services;

public enum OrdersBoardBulkAdvanceOutcome
{
    Success,
    Partial,
    Failure,
}

public enum OrdersBoardBulkActionType
{
    Advance,
    Retry,
}

public sealed record OrdersBoardBulkAdvanceResultSummary(
    OrdersBoardBulkAdvanceOutcome Outcome,
    string Message,
    int SuccessCount,
    int FailedCount);

public static class OrdersBoardBulkAdvanceResultBuilder
{
    public static OrdersBoardBulkAdvanceResultSummary Build(
        string groupTitle,
        int successCount,
        int failedCount,
        string? sampleFailedCustomerName = null,
        OrdersBoardBulkActionType actionType = OrdersBoardBulkActionType.Advance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupTitle);

        if (successCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(successCount));
        }

        if (failedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedCount));
        }

        if (failedCount == 0)
        {
            return new OrdersBoardBulkAdvanceResultSummary(
                OrdersBoardBulkAdvanceOutcome.Success,
                $"{GetSuccessVerb(actionType)} {successCount} order(s) in {groupTitle}.",
                successCount,
                failedCount);
        }

        if (successCount == 0)
        {
            return new OrdersBoardBulkAdvanceResultSummary(
                OrdersBoardBulkAdvanceOutcome.Failure,
                $"Unable to {GetFailureVerb(actionType)} orders in {groupTitle}.",
                successCount,
                failedCount);
        }

        var hasSample = !string.IsNullOrWhiteSpace(sampleFailedCustomerName);
        var message = hasSample
            ? $"{GetSuccessVerb(actionType)} {successCount} order(s) in {groupTitle}; {failedCount} failed (e.g. {sampleFailedCustomerName!.Trim()})."
            : $"{GetSuccessVerb(actionType)} {successCount} order(s) in {groupTitle}; {failedCount} failed.";

        return new OrdersBoardBulkAdvanceResultSummary(
            OrdersBoardBulkAdvanceOutcome.Partial,
            message,
            successCount,
            failedCount);
    }

    private static string GetSuccessVerb(OrdersBoardBulkActionType actionType)
    {
        return actionType switch
        {
            OrdersBoardBulkActionType.Advance => "Advanced",
            OrdersBoardBulkActionType.Retry => "Retried",
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, "Unsupported bulk action type."),
        };
    }

    private static string GetFailureVerb(OrdersBoardBulkActionType actionType)
    {
        return actionType switch
        {
            OrdersBoardBulkActionType.Advance => "advance",
            OrdersBoardBulkActionType.Retry => "retry",
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, "Unsupported bulk action type."),
        };
    }
}