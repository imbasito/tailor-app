using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrderWorklistResult(
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyList<OrderWorklistItemDto> Items)
{
    public static OrderWorklistResult Success(IReadOnlyList<OrderWorklistItemDto> items)
    {
        return new OrderWorklistResult(
            IsSuccess: true,
            ErrorMessage: null,
            Items: items);
    }

    public static OrderWorklistResult Failure(string errorMessage)
    {
        return new OrderWorklistResult(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            Items: []);
    }
}
