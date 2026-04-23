using System.Text.Json;

namespace STailor.UI.Rcl.Services;

public sealed class LocalBackupRestoreService : IBackupRestoreService
{
    private const string ManifestFileName = "backup-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;
    private readonly IReadOnlyList<string> _databaseFilePaths;

    public LocalBackupRestoreService(
        string? settingsFilePath = null,
        IEnumerable<string>? databaseFilePaths = null,
        string? backupRootPath = null)
    {
        var appDataPath = BuildAppDataPath();
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? Path.Combine(appDataPath, "workspace-settings.json")
            : settingsFilePath;
        _databaseFilePaths = (databaseFilePaths ?? BuildDatabaseCandidates())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        BackupRootPath = string.IsNullOrWhiteSpace(backupRootPath)
            ? Path.Combine(appDataPath, "Backups")
            : backupRootPath;
    }

    public string BackupRootPath { get; }

    public async Task<BackupRestoreResult> CreateBackupAsync(
        string? backupRootPath = null,
        CancellationToken cancellationToken = default)
    {
        var sourceFiles = BuildSourceFiles()
            .Where(source => File.Exists(source.Path))
            .ToArray();

        if (sourceFiles.Length == 0)
        {
            return new BackupRestoreResult(false, "No local settings or database files were found to back up.");
        }

        var backupPath = Path.Combine(
            string.IsNullOrWhiteSpace(backupRootPath) ? BackupRootPath : backupRootPath,
            $"backup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");

        Directory.CreateDirectory(backupPath);

        var manifestFiles = new List<BackupManifestFile>();
        foreach (var source in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.Combine(source.Kind, BuildBackupFileName(source.Path));
            var targetPath = Path.Combine(backupPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(source.Path, targetPath, overwrite: false);
            manifestFiles.Add(new BackupManifestFile(source.Kind, source.Path, relativePath));
        }

        var manifest = new BackupManifest(DateTimeOffset.UtcNow, manifestFiles);
        var manifestJson = JsonSerializer.Serialize(manifest, SerializerOptions);
        await File.WriteAllTextAsync(Path.Combine(backupPath, ManifestFileName), manifestJson, cancellationToken);

        return new BackupRestoreResult(
            true,
            $"Backup created with {manifestFiles.Count} file(s).",
            backupPath,
            manifestFiles.Count);
    }

    public async Task<BackupRestoreResult> RestoreBackupAsync(
        string manifestFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestFilePath))
        {
            return new BackupRestoreResult(false, "Choose a backup manifest file before restoring.");
        }

        if (!File.Exists(manifestFilePath))
        {
            return new BackupRestoreResult(false, "The selected restore file was not found.");
        }

        var backupPath = Path.GetDirectoryName(manifestFilePath);
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return new BackupRestoreResult(false, "The selected restore file path is invalid.");
        }

        BackupManifest? manifest;
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestFilePath, cancellationToken);
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, SerializerOptions);
        }
        catch (JsonException)
        {
            return new BackupRestoreResult(false, "The selected backup manifest is damaged and cannot be restored.", backupPath);
        }

        if (manifest?.Files is null || manifest.Files.Count == 0)
        {
            return new BackupRestoreResult(false, "The selected backup does not contain any restorable files.", backupPath);
        }

        var restoredCount = 0;
        try
        {
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourcePath = Path.Combine(backupPath, file.RelativePath);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var targetDirectory = Path.GetDirectoryName(file.OriginalPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(sourcePath, file.OriginalPath, overwrite: true);
                restoredCount++;
            }
        }
        catch (IOException exception)
        {
            return new BackupRestoreResult(
                false,
                $"Restore could not finish because a file is in use. Close the app/API and try again. Details: {exception.Message}",
                backupPath,
                restoredCount);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new BackupRestoreResult(
                false,
                $"Restore could not access one of the files. Details: {exception.Message}",
                backupPath,
                restoredCount);
        }

        return new BackupRestoreResult(
            true,
            $"Restored {restoredCount} file(s) from the selected backup.",
            backupPath,
            restoredCount);
    }

    private IEnumerable<BackupSourceFile> BuildSourceFiles()
    {
        yield return new BackupSourceFile("settings", _settingsFilePath);

        foreach (var databaseFilePath in _databaseFilePaths)
        {
            yield return new BackupSourceFile("database", databaseFilePath);
        }
    }

    private static string BuildBackupFileName(string sourcePath)
    {
        var parentName = Path.GetFileName(Path.GetDirectoryName(sourcePath));
        var fileName = Path.GetFileName(sourcePath);
        var prefix = string.IsNullOrWhiteSpace(parentName) ? "local" : SanitizeFileName(parentName);
        return $"{prefix}-{SanitizeFileName(fileName)}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalidCharacters.Contains(character) ? '-' : character));
    }

    private static string BuildAppDataPath()
    {
        var rootPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(rootPath, "SINYXTailorManagement");
    }

    private static IReadOnlyList<string> BuildDatabaseCandidates()
    {
        var candidates = new List<string>();

        AddDatabaseCandidates(candidates, Directory.GetCurrentDirectory());
        AddDatabaseCandidates(candidates, AppContext.BaseDirectory);

        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (string.Equals(currentDirectory.Name, "modern", StringComparison.OrdinalIgnoreCase))
            {
                AddDatabaseCandidates(candidates, Path.Combine(currentDirectory.FullName, "src", "STailor.Api"));
                AddDatabaseCandidates(candidates, Path.Combine(currentDirectory.FullName, "src", "STailor.Api", "bin", "Debug", "net8.0"));
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return candidates;
    }

    private static void AddDatabaseCandidates(List<string> candidates, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        candidates.Add(Path.Combine(directoryPath, "stailor-local.db"));
        candidates.Add(Path.Combine(directoryPath, "stailor-local.dev.db"));
    }

    private sealed record BackupSourceFile(string Kind, string Path);

    private sealed record BackupManifest(DateTimeOffset CreatedAtUtc, IReadOnlyList<BackupManifestFile> Files);

    private sealed record BackupManifestFile(string Kind, string OriginalPath, string RelativePath);
}
