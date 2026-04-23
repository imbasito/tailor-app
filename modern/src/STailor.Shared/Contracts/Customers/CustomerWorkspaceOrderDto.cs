namespace STailor.Shared.Contracts.Customers;

public sealed record CustomerWorkspaceOrderDto(
    Guid OrderId,
    string GarmentType,
    string Status,
    decimal AmountCharged,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset DueAtUtc,
    string MeasurementSnapshotJson);
