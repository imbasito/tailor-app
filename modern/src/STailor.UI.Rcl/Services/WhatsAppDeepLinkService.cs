using System.Text;

namespace STailor.UI.Rcl.Services;

public sealed class WhatsAppDeepLinkService
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;

    public WhatsAppDeepLinkService(IExternalLinkLauncher externalLinkLauncher)
    {
        _externalLinkLauncher = externalLinkLauncher;
    }

    public async Task<WhatsAppLaunchResult> OpenChatAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePhoneNumber(phoneNumber, out var normalizedPhoneNumber))
        {
            return WhatsAppLaunchResult.Failure(
                "Phone number must include a country code and at least 8 digits.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return WhatsAppLaunchResult.Failure("Message is required.");
        }

        var targetUri = BuildUri(normalizedPhoneNumber, message.Trim());

        try
        {
            var opened = await _externalLinkLauncher.OpenAsync(targetUri, cancellationToken);
            return opened
                ? WhatsAppLaunchResult.Success()
                : WhatsAppLaunchResult.Failure("Unable to open WhatsApp on this device.");
        }
        catch (Exception exception)
        {
            return WhatsAppLaunchResult.Failure($"Unable to open WhatsApp: {exception.Message}");
        }
    }

    internal static Uri BuildUri(string normalizedPhoneNumber, string message)
    {
        var encodedPhoneNumber = Uri.EscapeDataString(normalizedPhoneNumber);
        var encodedMessage = Uri.EscapeDataString(message);

        return new Uri($"whatsapp://send?phone={encodedPhoneNumber}&text={encodedMessage}");
    }

    internal static bool TryNormalizePhoneNumber(string phoneNumber, out string normalizedPhoneNumber)
    {
        normalizedPhoneNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        var buffer = new StringBuilder(phoneNumber.Length);
        foreach (var character in phoneNumber)
        {
            if (char.IsDigit(character))
            {
                buffer.Append(character);
            }
        }

        if (buffer.Length < 8)
        {
            return false;
        }

        normalizedPhoneNumber = buffer.ToString();
        return true;
    }
}
