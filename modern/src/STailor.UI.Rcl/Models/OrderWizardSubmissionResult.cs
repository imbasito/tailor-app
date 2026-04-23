namespace STailor.UI.Rcl.Models;

public sealed record OrderWizardSubmissionResult(
    bool IsSuccess,
    string? ErrorMessage,
    Guid? CustomerId,
    Guid? OrderId,
    string? FinalStatus,
    string? CustomerName,
    string? PhoneNumber,
    DateTimeOffset? DueAtUtc,
    decimal? BalanceDue)
{
    public static OrderWizardSubmissionResult Success(
        Guid customerId,
        Guid orderId,
        string finalStatus,
        string customerName,
        string phoneNumber,
        DateTimeOffset dueAtUtc,
        decimal balanceDue)
    {
        return new OrderWizardSubmissionResult(
            IsSuccess: true,
            ErrorMessage: null,
            CustomerId: customerId,
            OrderId: orderId,
            FinalStatus: finalStatus,
            CustomerName: customerName,
            PhoneNumber: phoneNumber,
            DueAtUtc: dueAtUtc,
            BalanceDue: balanceDue);
    }

    public static OrderWizardSubmissionResult Failure(string errorMessage)
    {
        return new OrderWizardSubmissionResult(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            CustomerId: null,
            OrderId: null,
            FinalStatus: null,
            CustomerName: null,
            PhoneNumber: null,
            DueAtUtc: null,
            BalanceDue: null);
    }
}
