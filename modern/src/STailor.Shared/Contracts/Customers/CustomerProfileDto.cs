namespace STailor.Shared.Contracts.Customers;

public sealed record CustomerProfileDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    string BaselineMeasurementsJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
