namespace STailor.Shared.Contracts.Orders;

public sealed record OrderPaymentHistoryItemDto(
    Guid PaymentId,
    decimal Amount,
    DateTimeOffset PaidAtUtc,
    string? Note);
