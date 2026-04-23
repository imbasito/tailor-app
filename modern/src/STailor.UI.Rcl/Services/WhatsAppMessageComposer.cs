using System.Globalization;

namespace STailor.UI.Rcl.Services;

public sealed class WhatsAppMessageComposer
{
    private readonly WorkspaceSettingsService? _workspaceSettings;

    public WhatsAppMessageComposer(WorkspaceSettingsService? workspaceSettings = null)
    {
        _workspaceSettings = workspaceSettings;
    }

    public string BuildOrderUpdateMessage(
        string? customerName,
        Guid? orderId,
        string? status,
        DateTimeOffset? dueAtUtc,
        decimal? balanceDue)
    {
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "dear customer" : customerName.Trim();
        var displayOrder = orderId?.ToString() ?? "N/A";
        var displayStatus = string.IsNullOrWhiteSpace(status) ? "in progress" : status.Trim();
        var displayDueDate = dueAtUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "TBD";
        var displayBalance = FormatCurrency(balanceDue);

        return
            $"Selam {displayName}, this is SINYX Tailor Management. " +
            $"Order {displayOrder} is now {displayStatus}. " +
            $"Due date: {displayDueDate}. Outstanding balance: {displayBalance}." +
            BuildSignature();
    }

    public string BuildDueBalanceReminder(
        string? customerName,
        Guid? orderId,
        DateTimeOffset? dueAtUtc,
        decimal? balanceDue)
    {
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "dear customer" : customerName.Trim();
        var displayOrder = orderId?.ToString() ?? "N/A";
        var displayDueDate = dueAtUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "TBD";
        var displayBalance = FormatCurrency(balanceDue);

        return
            $"Selam {displayName}, friendly reminder from SINYX Tailor Management. " +
            $"Order {displayOrder} is due on {displayDueDate}. " +
            $"Current balance due is {displayBalance}. " +
            "Please reply with your preferred pickup and payment time." +
            BuildSignature();
    }

    private string BuildSignature()
    {
        var productName = _workspaceSettings?.ProductName;
        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = WorkspaceSettingsService.DefaultProductName;
        }

        var displayName = productName.Trim();
        if (!displayName.EndsWith("Tailor", StringComparison.OrdinalIgnoreCase)
            && !displayName.EndsWith("Tailors", StringComparison.OrdinalIgnoreCase))
        {
            displayName += " Tailors";
        }

        return $"\n\n*Powered by {displayName}*";
    }

    private static string FormatCurrency(decimal? amount)
    {
        var rounded = decimal.Round(Math.Max(0m, amount ?? 0m), 2, MidpointRounding.AwayFromZero);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} ETB", rounded);
    }
}
