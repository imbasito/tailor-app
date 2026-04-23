namespace STailor.Shared.Contracts.Orders;

public sealed record OrderWorkspaceDetailDto(
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
    IReadOnlyList<OrderPaymentHistoryItemDto> Payments);
