namespace STailor.Core.Application.ReadModels;

public sealed record OrderReminderCandidate(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string PhoneNumber,
    string GarmentType,
    string Status,
    decimal AmountCharged,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset DueAtUtc);
