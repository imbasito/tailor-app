namespace STailor.Shared.Contracts.Orders;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string GarmentType,
    string Status,
    decimal AmountCharged,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset DueAtUtc,
    string MeasurementSnapshotJson,
    DateTimeOffset? TrialScheduledAtUtc = null,
    string? TrialScheduleStatus = null,
    string PhotoAttachmentsJson = "[]");
