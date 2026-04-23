namespace STailor.UI.Rcl.Services;

public interface IBackupRestoreDialogService
{
    Task<string?> PickBackupFolderAsync(string currentPath, CancellationToken cancellationToken = default);

    Task<string?> PickRestoreManifestAsync(string currentPath, CancellationToken cancellationToken = default);
}

public sealed class UnavailableBackupRestoreDialogService : IBackupRestoreDialogService
{
    public Task<string?> PickBackupFolderAsync(string currentPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickRestoreManifestAsync(string currentPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
