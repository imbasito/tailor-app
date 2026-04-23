using Microsoft.JSInterop;
using STailor.UI.Rcl.Services;

namespace STailor.Web.Services;

public sealed class BrowserExternalLinkLauncher : IExternalLinkLauncher
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserExternalLinkLauncher(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("open", cancellationToken, uri.ToString(), "_blank");
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
