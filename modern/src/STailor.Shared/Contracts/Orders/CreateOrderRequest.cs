namespace STailor.Shared.Contracts.Orders;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    string GarmentType,
    IReadOnlyDictionary<string, decimal>? OverrideMeasurements,
    decimal AmountCharged,
    decimal InitialDeposit,
    DateTimeOffset DueAtUtc,
    IReadOnlyList<OrderPhotoAttachmentDto>? PhotoAttachments = null,
    DateTimeOffset? TrialScheduledAtUtc = null,
    string? TrialScheduleStatus = null,
    bool ApplyTrialStatusTransition = false);
