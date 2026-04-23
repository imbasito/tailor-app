namespace STailor.Core.Application.Abstractions.Services;

public interface IMeasurementService
{
    IReadOnlyDictionary<string, decimal> MergeMeasurements(
        IReadOnlyDictionary<string, decimal> baseline,
        IReadOnlyDictionary<string, decimal>? overrides);

    string Serialize(IReadOnlyDictionary<string, decimal> measurements);

    IReadOnlyDictionary<string, decimal> Deserialize(string json);
}
