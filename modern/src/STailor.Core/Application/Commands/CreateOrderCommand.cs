namespace STailor.Core.Application.Commands;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    string GarmentType,
    IReadOnlyDictionary<string, decimal>? OverrideMeasurements,
    decimal AmountCharged,
    decimal InitialDeposit,
    DateTimeOffset DueAtUtc,
    IReadOnlyList<CreateOrderPhotoAttachmentCommand>? PhotoAttachments = null,
    DateTimeOffset? TrialScheduledAtUtc = null,
    string? TrialScheduleStatus = null,
    bool ApplyTrialStatusTransition = false);
