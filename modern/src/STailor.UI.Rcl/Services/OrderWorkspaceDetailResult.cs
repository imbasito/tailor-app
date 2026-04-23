using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed record OrderWorkspaceDetailResult(
    bool IsSuccess,
    string? ErrorMessage,
    OrderWorkspaceDetailDto? Order)
{
    public static OrderWorkspaceDetailResult Success(OrderWorkspaceDetailDto order)
    {
        return new OrderWorkspaceDetailResult(true, null, order);
    }

    public static OrderWorkspaceDetailResult Failure(string errorMessage)
    {
        return new OrderWorkspaceDetailResult(false, errorMessage, null);
    }
}
