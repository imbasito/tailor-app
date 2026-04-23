namespace STailor.Core.Application.Migration;

public sealed record LegacyMigrationIssue(
    string EntityType,
    int LegacyId,
    string Message);
