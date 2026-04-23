namespace STailor.Core.Application.Commands;

public sealed record UpsertBaselineMeasurementsCommand(
    Guid CustomerId,
    string GarmentType,
    IReadOnlyDictionary<string, decimal> Measurements);
