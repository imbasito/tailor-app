namespace STailor.UI.Rcl.Services;

public sealed class WorkspaceSettingsService
{
    public const string DefaultApiBaseUrl = "http://localhost:5064";
    public const string DefaultProductName = "SINYX Tailor Management";
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<GarmentMeasurementDefault>> DefaultMeasurementDefaults =
        new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Suit"] =
            [
                new("Chest", 40m),
                new("Waist", 32m),
                new("Hip", 38m),
                new("Sleeve", 24m),
                new("Shoulder", 18m),
            ],
            ["Shirt"] =
            [
                new("Neck", 15m),
                new("Chest", 40m),
                new("Sleeve", 24m),
                new("Shirt Length", 30m),
            ],
            ["Trouser"] =
            [
                new("Waist", 32m),
                new("Hip", 38m),
                new("Length", 40m),
                new("Bottom", 14m),
            ],
            ["Coat"] =
            [
                new("Chest", 40m),
                new("Waist", 34m),
                new("Shoulder", 18m),
                new("Sleeve", 25m),
            ],
            ["Kurta"] =
            [
                new("Chest", 40m),
                new("Shoulder", 18m),
                new("Sleeve", 23m),
                new("Length", 42m),
            ],
            ["Waistcoat"] =
            [
                new("Chest", 40m),
                new("Waist", 34m),
                new("Length", 24m),
            ],
            ["General"] =
            [
                new("Length", 40m),
                new("Chest", 40m),
            ],
        };
    public static readonly IReadOnlyList<string> DefaultGarmentTypes = DefaultMeasurementDefaults.Keys.ToArray();

    private readonly IWorkspaceSettingsStore? _store;
    private bool _isConfigured;

    public WorkspaceSettingsService(IWorkspaceSettingsStore? store = null)
    {
        _store = store;

        if (_store is null)
        {
            return;
        }

        var snapshot = _store.Load();
        ApiBaseUrl = snapshot.ApiBaseUrl;
        ProductName = snapshot.ProductName;
        LogoDataUrl = snapshot.LogoDataUrl;
        ShopAddress = snapshot.ShopAddress ?? string.Empty;
        ShopPhoneNumber = snapshot.ShopPhoneNumber ?? string.Empty;
        NationalTaxNumber = snapshot.NationalTaxNumber ?? string.Empty;
        SalesTaxRegistrationNumber = snapshot.SalesTaxRegistrationNumber ?? string.Empty;
        _isConfigured = snapshot.IsConfigured;
        MeasurementDefaults = NormalizeMeasurementDefaults(snapshot.MeasurementDefaults, snapshot.GarmentTypes);
    }

    public string ApiBaseUrl { get; private set; } = DefaultApiBaseUrl;
    public string ProductName { get; private set; } = DefaultProductName;
    public string? LogoDataUrl { get; private set; }
    public string ShopAddress { get; private set; } = string.Empty;
    public string ShopPhoneNumber { get; private set; } = string.Empty;
    public string NationalTaxNumber { get; private set; } = string.Empty;
    public string SalesTaxRegistrationNumber { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, IReadOnlyList<GarmentMeasurementDefault>> MeasurementDefaults { get; private set; } = DefaultMeasurementDefaults;
    public IReadOnlyList<string> GarmentTypes => MeasurementDefaults.Keys.ToArray();
    public bool NeedsSetup => !_isConfigured;
    public event Action? Changed;

    public void ApplyApiBaseUrlOverride(string? apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            UpdateConnection(apiBaseUrl);
        }
    }

    public void UpdateConnection(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new ArgumentException("API base URL is required.", nameof(apiBaseUrl));
        }

        var normalizedApiBaseUrl = apiBaseUrl.Trim();
        if (!Uri.TryCreate(normalizedApiBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("API base URL must be a valid absolute URL.", nameof(apiBaseUrl));
        }

        ApiBaseUrl = normalizedApiBaseUrl.TrimEnd('/');
        Persist();
    }

    public void UpdateBranding(string productName)
    {
        ProductName = string.IsNullOrWhiteSpace(productName)
            ? DefaultProductName
            : productName.Trim();

        Persist();
    }

    public void UpdateLogo(string? logoDataUrl)
    {
        LogoDataUrl = string.IsNullOrWhiteSpace(logoDataUrl)
            ? null
            : logoDataUrl.Trim();

        Persist();
    }

    public void UpdateShopProfile(
        string? shopAddress,
        string? shopPhoneNumber,
        string? nationalTaxNumber,
        string? salesTaxRegistrationNumber)
    {
        ShopAddress = NormalizeOptional(shopAddress);
        ShopPhoneNumber = NormalizeOptional(shopPhoneNumber);
        NationalTaxNumber = NormalizeOptional(nationalTaxNumber);
        SalesTaxRegistrationNumber = NormalizeOptional(salesTaxRegistrationNumber);
        Persist();
    }

    public void UpdateGarmentTypes(IEnumerable<string> garmentTypes)
    {
        var updated = new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase);
        foreach (var garmentType in NormalizeGarmentTypes(garmentTypes))
        {
            updated[garmentType] = GetMeasurementDefaults(garmentType);
        }

        MeasurementDefaults = NormalizeMeasurementDefaults(updated, null);
        Persist();
    }

    public void UpdateMeasurementDefaults(
        string garmentType,
        IEnumerable<GarmentMeasurementDefault> defaults)
    {
        var normalizedGarmentType = NormalizeGarmentType(garmentType);
        var normalizedDefaults = NormalizeMeasurementRows(defaults);
        var updated = MeasurementDefaults.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        updated[normalizedGarmentType] = normalizedDefaults;
        MeasurementDefaults = NormalizeMeasurementDefaults(updated, null);
        Persist();
    }

    public void RemoveGarmentType(string garmentType)
    {
        var normalizedGarmentType = NormalizeGarmentType(garmentType);
        var updated = MeasurementDefaults
            .Where(pair => !string.Equals(pair.Key, normalizedGarmentType, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        MeasurementDefaults = NormalizeMeasurementDefaults(updated, null);
        Persist();
    }

    public IReadOnlyList<GarmentMeasurementDefault> GetMeasurementDefaults(string garmentType)
    {
        return MeasurementDefaults.TryGetValue(garmentType, out var defaults)
            ? defaults
            : DefaultMeasurementDefaults.TryGetValue(garmentType, out var builtInDefaults)
                ? builtInDefaults
                : DefaultMeasurementDefaults["General"];
    }

    public void ResetToDefaults()
    {
        ApiBaseUrl = DefaultApiBaseUrl;
        ProductName = DefaultProductName;
        LogoDataUrl = null;
        ShopAddress = string.Empty;
        ShopPhoneNumber = string.Empty;
        NationalTaxNumber = string.Empty;
        SalesTaxRegistrationNumber = string.Empty;
        MeasurementDefaults = DefaultMeasurementDefaults;
        _isConfigured = false;
        Persist(markConfigured: false);
    }

    private void Persist(bool markConfigured = true)
    {
        _store?.Save(new WorkspaceSettingsSnapshot(
            ApiBaseUrl,
            ProductName,
            LogoDataUrl,
            IsConfigured: markConfigured,
            GarmentTypes: GarmentTypes,
            MeasurementDefaults: MeasurementDefaults,
            ShopAddress: ShopAddress,
            ShopPhoneNumber: ShopPhoneNumber,
            NationalTaxNumber: NationalTaxNumber,
            SalesTaxRegistrationNumber: SalesTaxRegistrationNumber));
        _isConfigured = markConfigured;
        Changed?.Invoke();
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<GarmentMeasurementDefault>> NormalizeMeasurementDefaults(
        IReadOnlyDictionary<string, IReadOnlyList<GarmentMeasurementDefault>>? measurementDefaults,
        IEnumerable<string>? garmentTypes)
    {
        var normalized = new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase);
        var sourceDefaults = measurementDefaults
            ?? (garmentTypes is null
                ? DefaultMeasurementDefaults
                : new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase));

        foreach (var pair in sourceDefaults)
        {
            var garmentType = NormalizeGarmentType(pair.Key);
            normalized[garmentType] = NormalizeMeasurementRows(pair.Value);
        }

        if (garmentTypes is not null)
        {
            foreach (var garmentType in NormalizeGarmentTypes(garmentTypes))
            {
                if (!normalized.ContainsKey(garmentType))
                {
                    normalized[garmentType] = DefaultMeasurementDefaults.TryGetValue(garmentType, out var defaults)
                        ? defaults
                        : DefaultMeasurementDefaults["General"];
                }
            }
        }

        return normalized.Count == 0
            ? DefaultMeasurementDefaults
            : normalized;
    }

    private static IReadOnlyList<string> NormalizeGarmentTypes(IEnumerable<string>? garmentTypes)
    {
        var normalized = (garmentTypes ?? DefaultGarmentTypes)
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0
            ? DefaultGarmentTypes
            : normalized;
    }

    private static string NormalizeGarmentType(string garmentType)
    {
        if (string.IsNullOrWhiteSpace(garmentType))
        {
            throw new ArgumentException("Garment type name is required.", nameof(garmentType));
        }

        return garmentType.Trim();
    }

    private static IReadOnlyList<GarmentMeasurementDefault> NormalizeMeasurementRows(IEnumerable<GarmentMeasurementDefault> defaults)
    {
        var normalized = defaults
            .Where(row => !string.IsNullOrWhiteSpace(row.Name) && row.Value > 0m)
            .GroupBy(row => row.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new GarmentMeasurementDefault(group.Key, group.Last().Value))
            .ToArray();

        return normalized.Length == 0
            ? DefaultMeasurementDefaults["General"]
            : normalized;
    }
}
