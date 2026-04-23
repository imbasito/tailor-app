namespace STailor.Core.Application.ReadModels;

public sealed record OrderWorklistItem(
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
    DateTimeOffset? TrialScheduledAtUtc = null,
    string? TrialScheduleStatus = null);
