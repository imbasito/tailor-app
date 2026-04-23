using STailor.UI.Rcl.Services;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class WorkspaceSettingsServiceTests
{
    [Fact]
    public void Defaults_AreClientFriendly()
    {
        var service = new WorkspaceSettingsService();

        Assert.Equal("http://localhost:5064", service.ApiBaseUrl);
        Assert.Equal("SINYX Tailor Management", service.ProductName);
        Assert.Null(service.LogoDataUrl);
        Assert.Contains("Suit", service.GarmentTypes);
        Assert.True(service.NeedsSetup);
    }

    [Fact]
    public void UpdateMethods_ApplyTrimmedValues()
    {
        var service = new WorkspaceSettingsService();

        service.UpdateConnection("  http://localhost:7001/  ");
        service.UpdateBranding("  Client Shop  ");
        service.UpdateLogo("  data:image/png;base64,AAA  ");
        service.UpdateShopProfile("  Main Bazaar Lahore  ", "  +92 300 1234567  ", "  1234567-8  ", "  3277876111111  ");
        service.UpdateGarmentTypes(["  Suit  ", "Jacket", "suit"]);

        Assert.Equal("http://localhost:7001", service.ApiBaseUrl);
        Assert.Equal("Client Shop", service.ProductName);
        Assert.Equal("data:image/png;base64,AAA", service.LogoDataUrl);
        Assert.Equal("Main Bazaar Lahore", service.ShopAddress);
        Assert.Equal("+92 300 1234567", service.ShopPhoneNumber);
        Assert.Equal("1234567-8", service.NationalTaxNumber);
        Assert.Equal("3277876111111", service.SalesTaxRegistrationNumber);
        Assert.Equal(["Suit", "Jacket"], service.GarmentTypes);
    }

    [Fact]
    public void UpdateBranding_BlankValuesResetToDefaults()
    {
        var service = new WorkspaceSettingsService();

        service.UpdateBranding("");

        Assert.Equal(WorkspaceSettingsService.DefaultProductName, service.ProductName);
    }

    [Fact]
    public void UpdateConnection_WithInvalidUri_Throws()
    {
        var service = new WorkspaceSettingsService();

        Assert.Throws<ArgumentException>(() => service.UpdateConnection("not-a-url"));
    }

    [Fact]
    public void ResetToDefaults_RestoresOriginalValues()
    {
        var service = new WorkspaceSettingsService();

        service.UpdateConnection("http://localhost:7001");
        service.UpdateBranding("Client Shop");

        service.ResetToDefaults();

        Assert.Equal(WorkspaceSettingsService.DefaultApiBaseUrl, service.ApiBaseUrl);
        Assert.Equal(WorkspaceSettingsService.DefaultProductName, service.ProductName);
        Assert.Null(service.LogoDataUrl);
        Assert.Equal(string.Empty, service.ShopAddress);
        Assert.Equal(string.Empty, service.ShopPhoneNumber);
        Assert.Equal(string.Empty, service.NationalTaxNumber);
        Assert.Equal(string.Empty, service.SalesTaxRegistrationNumber);
        Assert.Equal(WorkspaceSettingsService.DefaultGarmentTypes, service.GarmentTypes);
        Assert.True(service.NeedsSetup);
    }

    [Fact]
    public void Constructor_LoadsExistingSnapshot()
    {
        var store = new InMemoryWorkspaceSettingsStore
        {
            Snapshot = new WorkspaceSettingsSnapshot(
                "http://localhost:7001",
                "Client Shop",
                "data:image/png;base64,AAA",
                true,
                ["Jacket", "Vest"]),
        };

        var service = new WorkspaceSettingsService(store);

        Assert.Equal("http://localhost:7001", service.ApiBaseUrl);
        Assert.Equal("Client Shop", service.ProductName);
        Assert.Equal("data:image/png;base64,AAA", service.LogoDataUrl);
        Assert.Equal(["Jacket", "Vest"], service.GarmentTypes);
        Assert.False(service.NeedsSetup);
    }

    [Fact]
    public void UpdateMethods_PersistSnapshot()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        var service = new WorkspaceSettingsService(store);

        service.UpdateConnection("http://localhost:7001");
        service.UpdateBranding("Client Shop");
        service.UpdateLogo("data:image/png;base64,AAA");
        service.UpdateGarmentTypes(["Jacket", "Vest"]);

        Assert.NotNull(store.Snapshot);
        Assert.Equal("http://localhost:7001", store.Snapshot!.ApiBaseUrl);
        Assert.Equal("Client Shop", store.Snapshot.ProductName);
        Assert.Equal("data:image/png;base64,AAA", store.Snapshot.LogoDataUrl);
        Assert.Equal(["Jacket", "Vest"], store.Snapshot.GarmentTypes);
        Assert.True(store.Snapshot.IsConfigured);
    }

    [Fact]
    public void UpdateMeasurementDefaults_AddsGarmentWithOwnRows()
    {
        var service = new WorkspaceSettingsService();

        service.UpdateMeasurementDefaults(
            " Sherwani ",
            [
                new GarmentMeasurementDefault(" Length ", 44m),
                new GarmentMeasurementDefault("Chest", 41m),
            ]);

        Assert.Contains("Sherwani", service.GarmentTypes);
        Assert.Equal(
            [
                new GarmentMeasurementDefault("Length", 44m),
                new GarmentMeasurementDefault("Chest", 41m),
            ],
            service.GetMeasurementDefaults("Sherwani"));
    }

    [Fact]
    public void Constructor_BackfillsMeasurementsForLegacyGarmentTypes()
    {
        var store = new InMemoryWorkspaceSettingsStore
        {
            Snapshot = new WorkspaceSettingsSnapshot(
                "http://localhost:7001",
                "Client Shop",
                GarmentTypes: ["Jacket", "Vest"]),
        };

        var service = new WorkspaceSettingsService(store);

        Assert.Equal(["Jacket", "Vest"], service.GarmentTypes);
        Assert.Equal(WorkspaceSettingsService.DefaultMeasurementDefaults["General"], service.GetMeasurementDefaults("Jacket"));
    }

    [Fact]
    public void UpdateMethods_RaiseChangedEvent()
    {
        var service = new WorkspaceSettingsService();
        var raisedCount = 0;

        service.Changed += () => raisedCount++;

        service.UpdateBranding("Client Shop");
        service.UpdateLogo("data:image/png;base64,AAA");
        service.UpdateConnection("http://localhost:7001");
        service.UpdateGarmentTypes(["Suit", "Jacket"]);

        Assert.Equal(4, raisedCount);
    }

    [Fact]
    public void UpdateMethods_ClearFirstRunSetupState()
    {
        var service = new WorkspaceSettingsService();

        service.UpdateBranding("Client Shop");

        Assert.False(service.NeedsSetup);
    }

    private sealed class InMemoryWorkspaceSettingsStore : IWorkspaceSettingsStore
    {
        public WorkspaceSettingsSnapshot? Snapshot { get; set; }

        public WorkspaceSettingsSnapshot Load()
        {
            return Snapshot ?? new WorkspaceSettingsSnapshot(
                WorkspaceSettingsService.DefaultApiBaseUrl,
                WorkspaceSettingsService.DefaultProductName);
        }

        public void Save(WorkspaceSettingsSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }
}
