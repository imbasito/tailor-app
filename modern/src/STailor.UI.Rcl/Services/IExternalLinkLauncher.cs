namespace STailor.UI.Rcl.Services;

public interface IExternalLinkLauncher
{
    Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}
