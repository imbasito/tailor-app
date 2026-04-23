namespace STailor.Core.Application.ReadModels;

public sealed record OrderWorkspaceDetail(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string PhoneNumber,
    string City,
    string GarmentType,
    string Status,
    decimal AmountCharged,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset DueAtUtc,
    string MeasurementSnapshotJson,
    string PhotoAttachmentsJson,
    DateTimeOffset? TrialScheduledAtUtc,
    string? TrialScheduleStatus,
    IReadOnlyList<OrderPaymentHistoryItem> Payments);
