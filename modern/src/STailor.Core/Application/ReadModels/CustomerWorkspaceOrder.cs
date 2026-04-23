namespace STailor.Core.Application.ReadModels;

public sealed record CustomerWorkspaceOrder(
    Guid OrderId,
    string GarmentType,
    string Status,
    decimal AmountCharged,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset DueAtUtc,
    string MeasurementSnapshotJson);
