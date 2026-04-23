namespace STailor.Core.Application.Commands;

public sealed record AddPaymentCommand(
    Guid OrderId,
    decimal Amount,
    DateTimeOffset? PaidAtUtc,
    string? Note);
