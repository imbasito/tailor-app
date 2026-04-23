namespace STailor.UI.Rcl.Services;

public interface IBackupRestoreService
{
    string BackupRootPath { get; }

    Task<BackupRestoreResult> CreateBackupAsync(
        string? backupRootPath = null,
        CancellationToken cancellationToken = default);

    Task<BackupRestoreResult> RestoreBackupAsync(
        string manifestFilePath,
        CancellationToken cancellationToken = default);
}

public sealed record BackupRestoreResult(
    bool IsSuccess,
    string Message,
    string? BackupPath = null,
    int FileCount = 0);
