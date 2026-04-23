namespace STailor.Core.Application.Migration;

public sealed record LegacyMigrationReport(
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
    IReadOnlyList<LegacyMigrationIssue> Issues)
{
    public bool HasIssues => Issues.Count > 0;
}
