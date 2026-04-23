using STailor.Modules.Core.Services;

namespace STailor.Modules.Core.Tests.Services;

public sealed class MeasurementServiceTests
{
    [Fact]
    public void MergeMeasurements_OverridesExistingValues()
    {
        var service = new MeasurementService();

        var baseline = new Dictionary<string, decimal>
        {
            ["Chest"] = 38m,
            ["Waist"] = 32m,
        };

        var overrides = new Dictionary<string, decimal>
        {
            ["Waist"] = 33m,
            ["Sleeve"] = 24m,
        };

        var merged = service.MergeMeasurements(baseline, overrides);

        Assert.Equal(3, merged.Count);
        Assert.Equal(38m, merged["Chest"]);
        Assert.Equal(33m, merged["Waist"]);
        Assert.Equal(24m, merged["Sleeve"]);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsMeasurements()
    {
        var service = new MeasurementService();

        var measurements = new Dictionary<string, decimal>
        {
            ["Length"] = 40.5m,
            ["Shoulder"] = 17m,
        };

        var json = service.Serialize(measurements);
        var parsed = service.Deserialize(json);

        Assert.Equal(measurements["Length"], parsed["Length"]);
        Assert.Equal(measurements["Shoulder"], parsed["Shoulder"]);
    }
}
