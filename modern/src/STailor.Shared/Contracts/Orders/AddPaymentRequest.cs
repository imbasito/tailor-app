namespace STailor.Shared.Contracts.Orders;

public sealed record AddPaymentRequest(
    decimal Amount,
    DateTimeOffset? PaidAtUtc,
    string? Note);
