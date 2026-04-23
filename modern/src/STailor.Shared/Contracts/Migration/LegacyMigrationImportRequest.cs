namespace STailor.Shared.Contracts.Migration;

public sealed record LegacyMigrationImportRequest(
    IReadOnlyList<LegacyCustomerMigrationDto> Customers,
    IReadOnlyList<LegacyOrderMigrationDto> Orders,
    bool ImportInactiveCustomers = false,
    bool ImportClosedOrders = false);
