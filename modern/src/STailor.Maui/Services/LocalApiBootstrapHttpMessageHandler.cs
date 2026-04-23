using STailor.UI.Rcl.Services;

namespace STailor.Maui.Services;

internal sealed class LocalApiBootstrapHttpMessageHandler : DelegatingHandler
{
    private readonly WorkspaceSettingsService _workspaceSettings;

    public LocalApiBootstrapHttpMessageHandler(WorkspaceSettingsService workspaceSettings)
        : base(new HttpClientHandler())
    {
        _workspaceSettings = workspaceSettings;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var targetUri = request.RequestUri;
        if (targetUri is not null)
        {
            await LocalApiBootstrapper.EnsureLocalApiAvailableAsync(
                targetUri.GetLeftPart(UriPartial.Authority),
                cancellationToken).ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
