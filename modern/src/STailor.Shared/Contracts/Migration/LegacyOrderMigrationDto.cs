namespace STailor.Shared.Contracts.Migration;

public sealed record LegacyOrderMigrationDto(
    int LegacyId,
    int LegacyCustomerId,
    string? Description,
    string? RecievedOn,
    string? AmountCharged,
    string? AmountPaid,
    string? CollectingOn,
    bool IsOpen = true);
