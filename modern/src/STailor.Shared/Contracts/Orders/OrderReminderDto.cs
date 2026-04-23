namespace STailor.Shared.Contracts.Orders;

public sealed record OrderReminderDto(
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
