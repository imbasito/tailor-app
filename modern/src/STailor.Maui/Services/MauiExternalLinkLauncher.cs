using Microsoft.Maui.ApplicationModel;
using STailor.UI.Rcl.Services;

namespace STailor.Maui.Services;

public sealed class MauiExternalLinkLauncher : IExternalLinkLauncher
{
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return Launcher.Default.OpenAsync(uri);
    }
}
