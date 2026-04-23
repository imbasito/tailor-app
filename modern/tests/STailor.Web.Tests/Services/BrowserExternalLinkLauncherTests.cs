using Microsoft.JSInterop;
using STailor.Web.Services;

namespace STailor.Web.Tests.Services;

public sealed class BrowserExternalLinkLauncherTests
{
    [Fact]
    public async Task OpenAsync_ReturnsTrue_WhenJsOpenSucceeds()
    {
        var jsRuntime = new FakeJsRuntime();
        var launcher = new BrowserExternalLinkLauncher(jsRuntime);
        var uri = new Uri("https://example.com");

        var result = await launcher.OpenAsync(uri);

        Assert.True(result);
        Assert.Equal("open", jsRuntime.Identifier);
        Assert.Equal(uri.ToString(), jsRuntime.Arguments![0]);
        Assert.Equal("_blank", jsRuntime.Arguments[1]);
    }

    [Fact]
    public async Task OpenAsync_ReturnsFalse_WhenJsThrows()
    {
        var jsRuntime = new FakeJsRuntime(shouldThrow: true);
        var launcher = new BrowserExternalLinkLauncher(jsRuntime);

        var result = await launcher.OpenAsync(new Uri("https://example.com"));

        Assert.False(result);
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly bool _shouldThrow;

        public FakeJsRuntime(bool shouldThrow = false)
        {
            _shouldThrow = shouldThrow;
        }

        public string? Identifier { get; private set; }

        public IReadOnlyList<object?>? Arguments { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (_shouldThrow)
            {
                throw new JSException("Blocked");
            }

            Identifier = identifier;
            Arguments = args ?? [];
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
