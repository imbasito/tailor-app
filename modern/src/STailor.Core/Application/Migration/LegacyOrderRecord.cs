namespace STailor.Core.Application.Migration;

public sealed record LegacyOrderRecord(
    int LegacyId,
    int LegacyCustomerId,
    string? Description,
    string? RecievedOn,
    string? AmountCharged,
    string? AmountPaid,
    string? CollectingOn,
    bool IsOpen = true);
