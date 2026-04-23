using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrderReminderWorklistResult(
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyList<OrderReminderDto> Items)
{
    public static OrderReminderWorklistResult Success(IReadOnlyList<OrderReminderDto> items)
    {
        return new OrderReminderWorklistResult(
            IsSuccess: true,
            ErrorMessage: null,
            Items: items);
    }

    public static OrderReminderWorklistResult Failure(string errorMessage)
    {
        return new OrderReminderWorklistResult(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            Items: []);
    }
}
