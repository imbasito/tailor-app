namespace STailor.Shared.Contracts.Customers;

public sealed record CustomerWorkspaceItemDto(
    Guid CustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    int OrderCount,
    decimal OutstandingBalance,
    DateTimeOffset? LastOrderReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc);
