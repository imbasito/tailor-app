using STailor.Shared.Contracts.Customers;

namespace STailor.UI.Rcl.Services;

public sealed record CustomerWorkspaceListResult(
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyList<CustomerWorkspaceItemDto> Items)
{
    public static CustomerWorkspaceListResult Success(IReadOnlyList<CustomerWorkspaceItemDto> items)
    {
        return new CustomerWorkspaceListResult(true, null, items);
    }

    public static CustomerWorkspaceListResult Failure(string errorMessage)
    {
        return new CustomerWorkspaceListResult(false, errorMessage, []);
    }
}
