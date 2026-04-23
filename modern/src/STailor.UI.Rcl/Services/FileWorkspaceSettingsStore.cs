using System.Text.Json;

namespace STailor.UI.Rcl.Services;

public sealed class FileWorkspaceSettingsStore : IWorkspaceSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public FileWorkspaceSettingsStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? BuildDefaultFilePath()
            : filePath;
    }

    public WorkspaceSettingsSnapshot Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return BuildDefaultSnapshot();
            }

            var json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<WorkspaceSettingsSnapshot>(json, SerializerOptions);
            return Normalize(snapshot);
        }
        catch
        {
            return BuildDefaultSnapshot();
        }
    }

    public void Save(WorkspaceSettingsSnapshot snapshot)
    {
        var normalized = Normalize(snapshot);
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }

    private static WorkspaceSettingsSnapshot Normalize(WorkspaceSettingsSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return BuildDefaultSnapshot();
        }

        var apiBaseUrl = string.IsNullOrWhiteSpace(snapshot.ApiBaseUrl)
            ? WorkspaceSettingsService.DefaultApiBaseUrl
            : snapshot.ApiBaseUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out _))
        {
            apiBaseUrl = WorkspaceSettingsService.DefaultApiBaseUrl;
        }

        var productName = string.IsNullOrWhiteSpace(snapshot.ProductName)
            ? WorkspaceSettingsService.DefaultProductName
            : snapshot.ProductName.Trim();

        var logoDataUrl = string.IsNullOrWhiteSpace(snapshot.LogoDataUrl)
            ? null
            : snapshot.LogoDataUrl.Trim();
        var shopAddress = NormalizeOptional(snapshot.ShopAddress);
        var shopPhoneNumber = NormalizeOptional(snapshot.ShopPhoneNumber);
        var nationalTaxNumber = NormalizeOptional(snapshot.NationalTaxNumber);
        var salesTaxRegistrationNumber = NormalizeOptional(snapshot.SalesTaxRegistrationNumber);

        var garmentTypes = (snapshot.GarmentTypes ?? WorkspaceSettingsService.DefaultGarmentTypes)
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (garmentTypes.Length == 0)
        {
            garmentTypes = WorkspaceSettingsService.DefaultGarmentTypes.ToArray();
        }

        var measurementDefaults = new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase);
        var sourceDefaults = snapshot.MeasurementDefaults
            ?? garmentTypes.ToDictionary(
                garmentType => garmentType,
                garmentType => WorkspaceSettingsService.DefaultMeasurementDefaults.TryGetValue(garmentType, out var defaults)
                    ? defaults
                    : WorkspaceSettingsService.DefaultMeasurementDefaults["General"],
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in sourceDefaults)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var rows = pair.Value
                .Where(row => !string.IsNullOrWhiteSpace(row.Name) && row.Value > 0m)
                .Select(row => new GarmentMeasurementDefault(row.Name.Trim(), row.Value))
                .ToArray();

            measurementDefaults[pair.Key.Trim()] = rows.Length == 0
                ? WorkspaceSettingsService.DefaultMeasurementDefaults["General"]
                : rows;
        }

        foreach (var garmentType in garmentTypes)
        {
            if (!measurementDefaults.ContainsKey(garmentType))
            {
                measurementDefaults[garmentType] = WorkspaceSettingsService.DefaultMeasurementDefaults.TryGetValue(garmentType, out var defaults)
                    ? defaults
                    : WorkspaceSettingsService.DefaultMeasurementDefaults["General"];
            }
        }

        return new WorkspaceSettingsSnapshot(
            apiBaseUrl,
            productName,
            logoDataUrl,
            snapshot.IsConfigured,
            measurementDefaults.Keys.ToArray(),
            measurementDefaults,
            shopAddress,
            shopPhoneNumber,
            nationalTaxNumber,
            salesTaxRegistrationNumber);
    }

    private static WorkspaceSettingsSnapshot BuildDefaultSnapshot()
    {
        return new WorkspaceSettingsSnapshot(
            WorkspaceSettingsService.DefaultApiBaseUrl,
            WorkspaceSettingsService.DefaultProductName,
            null,
            false,
            WorkspaceSettingsService.DefaultGarmentTypes,
            WorkspaceSettingsService.DefaultMeasurementDefaults,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildDefaultFilePath()
    {
        var rootPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(rootPath, "SINYXTailorManagement", "workspace-settings.json");
    }
}
