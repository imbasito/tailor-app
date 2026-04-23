namespace STailor.Shared.Contracts.Migration;

public sealed record LegacyCustomerMigrationDto(
    int LegacyId,
    string? FullName,
    string? Phone,
    string? City,
    string? Comment,
    bool IsActive = true);
