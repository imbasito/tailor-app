namespace STailor.UI.Rcl.Services;

public sealed record WhatsAppLaunchResult(bool IsSuccess, string? ErrorMessage)
{
    public static WhatsAppLaunchResult Success() => new(true, null);

    public static WhatsAppLaunchResult Failure(string errorMessage) => new(false, errorMessage);
}
