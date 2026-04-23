namespace STailor.Shared.Contracts.Migration;

public sealed record LegacyMigrationReportDto(
    int InputCustomerCount,
    int InputOrderCount,
    int FilteredCustomerCount,
    int FilteredOrderCount,
    int ImportedCustomerCount,
    int ImportedOrderCount,
    int SkippedInactiveCustomerCount,
    int SkippedClosedOrderCount,
    decimal SourceChargedTotal,
    decimal SourcePaidTotal,
    decimal ImportedChargedTotal,
    decimal ImportedPaidTotal,
    decimal ImportedBalanceTotal,
    IReadOnlyList<LegacyMigrationIssueDto> Issues);
