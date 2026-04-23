using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class WhatsAppMessageComposerTests
{
    [Fact]
    public void BuildOrderUpdateMessage_IncludesStatusDueDateAndBalance()
    {
        var composer = new WhatsAppMessageComposer();
        var orderId = Guid.Parse("7b8f6b0a-8a09-4f33-8414-31f9de783f11");

        var message = composer.BuildOrderUpdateMessage(
            customerName: "Amina Noor",
            orderId: orderId,
            status: "Ready",
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
            balanceDue: 1234.5m);

        Assert.Contains("Amina Noor", message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("Ready", message, StringComparison.Ordinal);
        Assert.Contains("2026-04-25", message, StringComparison.Ordinal);
        Assert.Contains("1234.50 ETB", message, StringComparison.Ordinal);
        Assert.Contains("*Powered by SINYX Tailor Management Tailors*", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDueBalanceReminder_WithMissingInputs_UsesFallbackValues()
    {
        var composer = new WhatsAppMessageComposer();

        var message = composer.BuildDueBalanceReminder(
            customerName: null,
            orderId: null,
            dueAtUtc: null,
            balanceDue: null);

        Assert.Contains("dear customer", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N/A", message, StringComparison.Ordinal);
        Assert.Contains("TBD", message, StringComparison.Ordinal);
        Assert.Contains("0.00 ETB", message, StringComparison.Ordinal);
        Assert.Contains("*Powered by SINYX Tailor Management Tailors*", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOrderUpdateMessage_UsesConfiguredProductNameInSignature()
    {
        var settings = new WorkspaceSettingsService();
        settings.UpdateBranding("A Khan");
        var composer = new WhatsAppMessageComposer(settings);

        var message = composer.BuildOrderUpdateMessage(
            customerName: "Amina Noor",
            orderId: null,
            status: "Ready",
            dueAtUtc: null,
            balanceDue: 0m);

        Assert.Contains("*Powered by A Khan Tailors*", message, StringComparison.Ordinal);
    }
}
