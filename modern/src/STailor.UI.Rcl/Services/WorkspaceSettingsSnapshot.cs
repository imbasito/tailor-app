namespace STailor.UI.Rcl.Services;

public sealed record WorkspaceSettingsSnapshot(
    string ApiBaseUrl,
    string ProductName,
    string? LogoDataUrl = null,
    bool IsConfigured = false,
    IReadOnlyList<string>? GarmentTypes = null,
    IReadOnlyDictionary<string, IReadOnlyList<GarmentMeasurementDefault>>? MeasurementDefaults = null,
    string? ShopAddress = null,
    string? ShopPhoneNumber = null,
    string? NationalTaxNumber = null,
    string? SalesTaxRegistrationNumber = null);
