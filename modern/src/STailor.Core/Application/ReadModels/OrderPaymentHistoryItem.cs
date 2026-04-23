namespace STailor.Core.Application.ReadModels;

public sealed record OrderPaymentHistoryItem(
    Guid PaymentId,
    decimal Amount,
    DateTimeOffset PaidAtUtc,
    string? Note);
