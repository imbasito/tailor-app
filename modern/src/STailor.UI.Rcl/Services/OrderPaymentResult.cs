using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrderPaymentResult(
    bool IsSuccess,
    string? ErrorMessage,
    OrderDto? Order)
{
    public static OrderPaymentResult Success(OrderDto order)
    {
        return new OrderPaymentResult(true, null, order);
    }

    public static OrderPaymentResult Failure(string errorMessage)
    {
        return new OrderPaymentResult(false, errorMessage, null);
    }
}
