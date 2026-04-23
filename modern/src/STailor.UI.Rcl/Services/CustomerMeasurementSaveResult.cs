using STailor.Shared.Contracts.Customers;

namespace STailor.UI.Rcl.Services;

public sealed record CustomerMeasurementSaveResult(
    bool IsSuccess,
    string? ErrorMessage,
    CustomerProfileDto? Customer)
{
    public static CustomerMeasurementSaveResult Success(CustomerProfileDto customer)
    {
        return new CustomerMeasurementSaveResult(true, null, customer);
    }

    public static CustomerMeasurementSaveResult Failure(string errorMessage)
    {
        return new CustomerMeasurementSaveResult(false, errorMessage, null);
    }
}
