using STailor.Shared.Contracts.Customers;

namespace STailor.UI.Rcl.Services;

public sealed record CustomerWorkspaceDetailResult(
    bool IsSuccess,
    string? ErrorMessage,
    CustomerWorkspaceDetailDto? Customer)
{
    public static CustomerWorkspaceDetailResult Success(CustomerWorkspaceDetailDto customer)
    {
        return new CustomerWorkspaceDetailResult(true, null, customer);
    }

    public static CustomerWorkspaceDetailResult Failure(string errorMessage)
    {
        return new CustomerWorkspaceDetailResult(false, errorMessage, null);
    }
}
