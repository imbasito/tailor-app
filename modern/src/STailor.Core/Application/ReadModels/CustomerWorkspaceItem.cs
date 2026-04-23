namespace STailor.Core.Application.ReadModels;

public sealed record CustomerWorkspaceItem(
    Guid CustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    int OrderCount,
    decimal OutstandingBalance,
    DateTimeOffset? LastOrderReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc);
