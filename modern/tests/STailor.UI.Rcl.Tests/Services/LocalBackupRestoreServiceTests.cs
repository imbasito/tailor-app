using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class LocalBackupRestoreServiceTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        "stailor-backup-restore-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateBackupAsync_WhenFilesExist_CreatesBackupFolder()
    {
        var settingsPath = WriteSourceFile("settings", "workspace-settings.json", "settings-json");
        var databasePath = WriteSourceFile("database", "stailor-local.dev.db", "database-bytes");
        var service = CreateService(settingsPath, databasePath);

        var result = await service.CreateBackupAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.FileCount);
        Assert.True(Directory.Exists(result.BackupPath));
        Assert.True(File.Exists(Path.Combine(result.BackupPath!, "backup-manifest.json")));
    }

    [Fact]
    public async Task RestoreLatestBackupAsync_RestoresSettingsAndDatabaseFiles()
    {
        var settingsPath = WriteSourceFile("settings", "workspace-settings.json", "original-settings");
        var databasePath = WriteSourceFile("database", "stailor-local.dev.db", "original-database");
        var service = CreateService(settingsPath, databasePath);
        var backupResult = await service.CreateBackupAsync();
        File.WriteAllText(settingsPath, "changed-settings");
        File.WriteAllText(databasePath, "changed-database");

        var result = await service.RestoreBackupAsync(Path.Combine(backupResult.BackupPath!, "backup-manifest.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal("original-settings", File.ReadAllText(settingsPath));
        Assert.Equal("original-database", File.ReadAllText(databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_WhenManifestIsMissing_ReturnsFailure()
    {
        var settingsPath = Path.Combine(_directoryPath, "settings", "workspace-settings.json");
        var databasePath = Path.Combine(_directoryPath, "database", "stailor-local.dev.db");
        var service = CreateService(settingsPath, databasePath);

        var result = await service.RestoreBackupAsync(Path.Combine(_directoryPath, "missing", "backup-manifest.json"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private LocalBackupRestoreService CreateService(string settingsPath, string databasePath)
    {
        return new LocalBackupRestoreService(
            settingsPath,
            [databasePath],
            Path.Combine(_directoryPath, "backups"));
    }

    private string WriteSourceFile(string folderName, string fileName, string content)
    {
        var directoryPath = Path.Combine(_directoryPath, folderName);
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
