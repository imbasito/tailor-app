namespace STailor.Shared.Contracts.Measurements;

public sealed record MeasurementSetDto(
    string GarmentType,
    IReadOnlyDictionary<string, decimal> Measurements);
