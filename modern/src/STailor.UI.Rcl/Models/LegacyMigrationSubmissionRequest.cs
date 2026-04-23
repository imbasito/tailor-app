namespace STailor.UI.Rcl.Models;

public sealed record LegacyMigrationSubmissionRequest(
    string ApiBaseUrl,
    string CustomersJson,
    string OrdersJson,
    bool ImportInactiveCustomers,
    bool ImportClosedOrders);
