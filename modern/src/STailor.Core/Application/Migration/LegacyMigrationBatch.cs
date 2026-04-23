namespace STailor.Core.Application.Migration;

public sealed record LegacyMigrationBatch(
    IReadOnlyList<LegacyCustomerRecord> Customers,
    IReadOnlyList<LegacyOrderRecord> Orders,
    bool ImportInactiveCustomers = false,
    bool ImportClosedOrders = false);
