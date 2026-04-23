using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class WhatsAppDeepLinkServiceTests
{
    [Fact]
    public async Task OpenChatAsync_WithValidInput_OpensWhatsAppUri()
    {
        var launcher = new FakeExternalLinkLauncher(openResult: true);
        var service = new WhatsAppDeepLinkService(launcher);

        var result = await service.OpenChatAsync(
            "+251 900-000-001",
            "Order is ready for pickup.");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        var targetUri = Assert.Single(launcher.RequestedUris);
        Assert.Equal("whatsapp", targetUri.Scheme);
        Assert.Equal("send", targetUri.Host);
        Assert.Equal("/", targetUri.AbsolutePath);
        Assert.Equal("?phone=251900000001&text=Order%20is%20ready%20for%20pickup.", targetUri.Query);
    }

    [Fact]
    public async Task OpenChatAsync_WithInvalidPhone_ReturnsFailureWithoutLaunch()
    {
        var launcher = new FakeExternalLinkLauncher(openResult: true);
        var service = new WhatsAppDeepLinkService(launcher);

        var result = await service.OpenChatAsync("12", "Status update");

        Assert.False(result.IsSuccess);
        Assert.Contains("Phone number", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(launcher.RequestedUris);
    }

    [Fact]
    public async Task OpenChatAsync_WhenLauncherCannotOpen_ReturnsFailure()
    {
        var launcher = new FakeExternalLinkLauncher(openResult: false);
        var service = new WhatsAppDeepLinkService(launcher);

        var result = await service.OpenChatAsync("+251900000001", "Status update");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unable to open WhatsApp", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(launcher.RequestedUris);
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        private readonly bool _openResult;

        public FakeExternalLinkLauncher(bool openResult)
        {
            _openResult = openResult;
        }

        public List<Uri> RequestedUris { get; } = new();

        public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            RequestedUris.Add(uri);
            return Task.FromResult(_openResult);
        }
    }
}
