using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class FileWorkspaceSettingsStoreTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        "stailor-workspace-settings-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsDefaults()
    {
        var store = CreateStore();

        var snapshot = store.Load();

        Assert.Equal(WorkspaceSettingsService.DefaultApiBaseUrl, snapshot.ApiBaseUrl);
        Assert.Equal(WorkspaceSettingsService.DefaultProductName, snapshot.ProductName);
        Assert.Null(snapshot.LogoDataUrl);
        Assert.Equal(WorkspaceSettingsService.DefaultGarmentTypes, snapshot.GarmentTypes);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSnapshot()
    {
        var store = CreateStore();
        var expected = new WorkspaceSettingsSnapshot(
            "http://localhost:7001",
            "Client Shop",
            GarmentTypes: ["Jacket", "Vest"],
            ShopAddress: "Main Bazaar Lahore",
            ShopPhoneNumber: "+92 300 1234567",
            NationalTaxNumber: "1234567-8",
            SalesTaxRegistrationNumber: "3277876111111");

        store.Save(expected);

        var snapshot = store.Load();

        Assert.Equal(expected.ApiBaseUrl, snapshot.ApiBaseUrl);
        Assert.Equal(expected.ProductName, snapshot.ProductName);
        Assert.Equal(expected.LogoDataUrl, snapshot.LogoDataUrl);
        Assert.Equal(expected.IsConfigured, snapshot.IsConfigured);
        Assert.Equal(expected.GarmentTypes, snapshot.GarmentTypes);
        Assert.Equal(expected.ShopAddress, snapshot.ShopAddress);
        Assert.Equal(expected.ShopPhoneNumber, snapshot.ShopPhoneNumber);
        Assert.Equal(expected.NationalTaxNumber, snapshot.NationalTaxNumber);
        Assert.Equal(expected.SalesTaxRegistrationNumber, snapshot.SalesTaxRegistrationNumber);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsMeasurementDefaults()
    {
        var store = CreateStore();
        var expectedDefaults = new Dictionary<string, IReadOnlyList<GarmentMeasurementDefault>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sherwani"] =
            [
                new GarmentMeasurementDefault("Length", 44m),
                new GarmentMeasurementDefault("Chest", 41m),
            ],
        };
        var expected = new WorkspaceSettingsSnapshot(
            "http://localhost:7001",
            "Client Shop",
            GarmentTypes: ["Sherwani"],
            MeasurementDefaults: expectedDefaults);

        store.Save(expected);

        var snapshot = store.Load();

        Assert.Equal(["Sherwani"], snapshot.GarmentTypes);
        Assert.NotNull(snapshot.MeasurementDefaults);
        Assert.Equal(expectedDefaults["Sherwani"], snapshot.MeasurementDefaults!["Sherwani"]);
    }

    [Fact]
    public void Load_WithInvalidFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_directoryPath);
        File.WriteAllText(GetFilePath(), "{ bad json");
        var store = CreateStore();

        var snapshot = store.Load();

        Assert.Equal(WorkspaceSettingsService.DefaultApiBaseUrl, snapshot.ApiBaseUrl);
        Assert.Equal(WorkspaceSettingsService.DefaultProductName, snapshot.ProductName);
        Assert.Null(snapshot.LogoDataUrl);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private FileWorkspaceSettingsStore CreateStore()
    {
        return new FileWorkspaceSettingsStore(GetFilePath());
    }

    private string GetFilePath()
    {
        return Path.Combine(_directoryPath, "workspace-settings.json");
    }
}
