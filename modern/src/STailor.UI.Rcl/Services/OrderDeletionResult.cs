namespace STailor.UI.Rcl.Services;

public sealed record OrderDeletionResult(
    bool IsSuccess,
    string? ErrorMessage)
{
    public static OrderDeletionResult Success()
    {
        return new OrderDeletionResult(
            IsSuccess: true,
            ErrorMessage: null);
    }

    public static OrderDeletionResult Failure(string errorMessage)
    {
        return new OrderDeletionResult(
            IsSuccess: false,
            ErrorMessage: errorMessage);
    }
}
