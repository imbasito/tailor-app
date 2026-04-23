namespace STailor.Shared.Contracts.Migration;

public sealed record LegacyMigrationIssueDto(
    string EntityType,
    int LegacyId,
    string Message);
