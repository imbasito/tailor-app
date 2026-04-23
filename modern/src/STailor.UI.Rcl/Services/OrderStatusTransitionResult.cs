using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrderStatusTransitionResult(
    bool IsSuccess,
    string? ErrorMessage,
    OrderDto? Order)
{
    public static OrderStatusTransitionResult Success(OrderDto order)
    {
        return new OrderStatusTransitionResult(
            IsSuccess: true,
            ErrorMessage: null,
            Order: order);
    }

    public static OrderStatusTransitionResult Failure(string errorMessage)
    {
        return new OrderStatusTransitionResult(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            Order: null);
    }
}
