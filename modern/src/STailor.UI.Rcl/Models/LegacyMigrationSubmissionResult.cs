using STailor.Shared.Contracts.Migration;

namespace STailor.UI.Rcl.Models;

public sealed record LegacyMigrationSubmissionResult(
    bool IsSuccess,
    string? ErrorMessage,
    LegacyMigrationReportDto? Report)
{
    public static LegacyMigrationSubmissionResult Success(LegacyMigrationReportDto report)
    {
        return new LegacyMigrationSubmissionResult(
            IsSuccess: true,
            ErrorMessage: null,
            Report: report);
    }

    public static LegacyMigrationSubmissionResult Failure(string errorMessage)
    {
        return new LegacyMigrationSubmissionResult(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            Report: null);
    }
}
