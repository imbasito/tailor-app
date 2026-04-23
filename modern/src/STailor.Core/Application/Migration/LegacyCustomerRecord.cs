namespace STailor.Core.Application.Migration;

public sealed record LegacyCustomerRecord(
    int LegacyId,
    string? FullName,
    string? Phone,
    string? City,
    string? Comment,
    bool IsActive = true);
